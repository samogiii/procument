using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Procument.Module.Identity.Entities;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Entities;
using Procument.Module.Purchasing.Services;
using Procument.Shared.Audit;

namespace Procument.Module.Purchasing.Controllers;

/// <summary>
/// Supplier quotes extracted from email by MailFlow, reviewed on the Procument page.
/// Import is for MailFlow — authenticated by the permanent X-Api-Key header (MailFlow:ApiKey in
/// appsettings), or an Admin/SuperAdmin login token. Review is limited to users granted the
/// "pendingSupplierQuotes" feature in Menu Access — SuperAdmin does NOT bypass (Sam's rule). Reviewers
/// only see lines from the mailboxes set on their user (Users page); SuperAdmin sees every mailbox.
/// </summary>
[ApiController]
[Route("api/pending-supplier-quotes")]
[Authorize]
public class PendingSupplierQuotesController : ControllerBase
{
    public const string Feature = "pendingSupplierQuotes";
    /// <summary>
    /// Accepting also adds the sender's address to the picked catalog supplier (Sam: only AMJ and GHS). Everyone
    /// else's accept just writes the supplier onto the RFQs. No SuperAdmin bypass.
    /// </summary>
    public const string SupplierEmailsFeature = "emailQuotesSupplierEmails";

    private readonly IPendingSupplierQuoteService _service;
    private readonly IPpeService _ppe;
    private readonly DbContext _db;
    private readonly IConfiguration _config;

    public PendingSupplierQuotesController(IPendingSupplierQuoteService service, IPpeService ppe, DbContext db, IConfiguration config)
    {
        _service = service;
        _ppe = ppe;
        _db = db;
        _config = config;
    }

    /// <summary>Login tokens expire after 7 days, so MailFlow uses a fixed key instead.</summary>
    private bool HasMailFlowApiKey()
    {
        var expected = _config["MailFlow:ApiKey"];
        var provided = Request.Headers["X-Api-Key"].ToString();
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(provided)) return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(provided));
    }

    private Task<bool> CanReviewAsync() => HasFeatureAsync(Feature);

    private async Task<bool> HasFeatureAsync(string feature)
    {
        var userName = User.Identity?.Name;
        return !string.IsNullOrEmpty(userName) && await _db.Set<MenuPermission>()
            .AnyAsync(p => p.Feature == feature && p.UserName == userName);
    }

    private ObjectResult Forbidden() =>
        StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not have access to pending supplier quotes." });

    private long GetUserId() =>
        long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

    private List<string>? _allowedMailboxes;
    private bool _allowedMailboxesLoaded;

    /// <summary>
    /// The mailboxes this user may see (Users page → Email mailboxes). null = every mailbox: SuperAdmin
    /// sees all mail (Sam). Anyone else with no mailboxes set sees nothing.
    /// </summary>
    private async Task<List<string>?> AllowedMailboxesAsync()
    {
        if (_allowedMailboxesLoaded) return _allowedMailboxes;
        _allowedMailboxesLoaded = true;
        if (User.IsInRole(UserRoles.SuperAdmin)) return _allowedMailboxes = null;
        var userId = GetUserId();
        return _allowedMailboxes = await _db.Set<UserMailbox>().AsNoTracking()
            .Where(m => m.UserId == userId).Select(m => m.Mailbox).ToListAsync();
    }

    private async Task<List<long>> VisibleIdsAsync(IEnumerable<long> ids) =>
        await _service.VisibleIdsAsync(ids, await AllowedMailboxesAsync());

    private async Task<bool> LineVisibleAsync(long id) => (await VisibleIdsAsync([id])).Count > 0;

    /// <summary>MailFlow posts one quote email at a time; an already-imported Message-ID is skipped.</summary>
    private bool IsMailFlow() => HasMailFlowApiKey() || User.IsInRole("Admin") || User.IsInRole("SuperAdmin");

    private UnauthorizedObjectResult MailFlowUnauthorized() =>
        Unauthorized(new { message = "Missing or wrong X-Api-Key (MailFlow:ApiKey in appsettings.json)." });

    [HttpPost("import")]
    [AllowAnonymous]
    [RequestSizeLimit(100_000_000)] // the email's attachments travel base64-encoded in the body
    public async Task<ActionResult<ImportPendingQuotesResponse>> Import([FromBody] ImportPendingQuotesRequest request)
    {
        if (!IsMailFlow()) return MailFlowUnauthorized();
        return Ok(await _service.ImportAsync(request));
    }

    /// <summary>MailFlow asks before running the AI on an email, so accepted/removed emails stay gone.</summary>
    [HttpGet("known")]
    [AllowAnonymous]
    public async Task<ActionResult<KnownEmailResponse>> Known([FromQuery] string messageId)
    {
        if (!IsMailFlow()) return MailFlowUnauthorized();
        if (string.IsNullOrWhiteSpace(messageId)) return Ok(new KnownEmailResponse());
        return Ok(await _service.GetKnownAsync(messageId));
    }

    /// <summary>
    /// Pending lines, one page of whole emails (?page=1&amp;pageSize=20); ?search= narrows by P/N or Alt P/N;
    /// ?value=under|over keeps lines whose Qty × Price is under / at least 5K.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PendingQuotePageResponse>> GetPending(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? mailbox = null,
        [FromQuery] string? value = null)
    {
        if (!await CanReviewAsync()) return Forbidden();
        return Ok(await _service.GetPendingPageAsync(search, page, pageSize, mailbox, await AllowedMailboxesAsync(), value));
    }

    /// <summary>The MailFlow mailboxes lines came in through (mailbox dropdown; "-" = unknown).</summary>
    [HttpGet("mailboxes")]
    public async Task<ActionResult<List<MailboxOption>>> Mailboxes()
    {
        if (!await CanReviewAsync()) return Forbidden();
        return Ok(await _service.GetMailboxesAsync(await AllowedMailboxesAsync()));
    }

    /// <summary>Which mailboxes the current user sees: all = SuperAdmin, else the ones set on the Users page.</summary>
    [HttpGet("my-mailboxes")]
    public async Task<ActionResult<MyMailboxesResponse>> MyMailboxes()
    {
        if (!await CanReviewAsync()) return Forbidden();
        var allowed = await AllowedMailboxesAsync();
        return Ok(new MyMailboxesResponse(allowed == null, allowed ?? []));
    }

    /// <summary>Accepted lines with who accepted them; ?days=30, ?userId=, ?search= to narrow; paged by email.</summary>
    [HttpGet("accepted")]
    public async Task<ActionResult<PendingQuotePageResponse>> GetAccepted(
        [FromQuery] int? days, [FromQuery] long? userId, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? mailbox = null)
    {
        if (!await CanReviewAsync()) return Forbidden();
        return Ok(await _service.GetAcceptedPageAsync(days, userId, search, page, pageSize, mailbox, await AllowedMailboxesAsync()));
    }

    /// <summary>Every accepted line the filters select, not paged — the Accepted tab's Excel export.</summary>
    [HttpGet("accepted/export")]
    public async Task<ActionResult<List<PendingSupplierQuoteResponse>>> ExportAccepted(
        [FromQuery] int? days, [FromQuery] long? userId, [FromQuery] string? search, [FromQuery] string? mailbox = null)
    {
        if (!await CanReviewAsync()) return Forbidden();
        return Ok(await _service.GetAcceptedExportAsync(days, userId, search, mailbox, await AllowedMailboxesAsync()));
    }

    [HttpPut("{id:long}")]
    [Auditable("PendingSupplierQuote", "Update", CaptureBody = true)]
    public async Task<ActionResult<PendingSupplierQuoteResponse>> Update(long id, [FromBody] UpdatePendingSupplierQuoteRequest request)
    {
        if (!await CanReviewAsync()) return Forbidden();
        if (!await LineVisibleAsync(id)) return NotFound();
        try
        {
            var result = await _service.UpdateAsync(id, request);
            return result == null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Supplier for a whole email's pending lines — the value only; the sender's address goes on the
    /// catalog supplier when a user with "emailQuotesSupplierEmails" accepts.
    /// </summary>
    [HttpPost("supplier")]
    [Auditable("PendingSupplierQuote", "SetSupplier", CaptureBody = true)]
    public async Task<ActionResult<SetPendingSupplierResponse>> SetSupplier([FromBody] SetPendingSupplierRequest request)
    {
        if (!await CanReviewAsync()) return Forbidden();
        try
        {
            return Ok(await _service.SetSupplierAsync(await VisibleIdsAsync(request.Ids), request.SupplierId));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Upload cert PDFs to a pending line — copied to every RFQ supplier quote on accept.</summary>
    [HttpPost("{id:long}/certificates")]
    [RequestSizeLimit(100_000_000)]
    public async Task<ActionResult<List<PendingQuoteCertificateResponse>>> UploadCertificates(long id, [FromForm] List<IFormFile> files)
    {
        if (!await CanReviewAsync()) return Forbidden();
        if (!await LineVisibleAsync(id)) return NotFound();
        try
        {
            var result = await _service.UploadCertificatesAsync(id, GetUserId(), files);
            return result == null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:long}/certificates/{certificateId:long}")]
    public async Task<IActionResult> GetCertificate(long id, long certificateId)
    {
        if (!await CanReviewAsync()) return Forbidden();
        if (!await LineVisibleAsync(id)) return NotFound();
        var file = await _service.GetCertificateAsync(id, certificateId);
        if (file == null) return NotFound();
        var (stream, fileName) = file.Value;
        Response.Headers["X-File-Name"] = Uri.EscapeDataString(fileName);
        return File(stream, "application/pdf");
    }

    [HttpDelete("{id:long}/certificates/{certificateId:long}")]
    public async Task<IActionResult> DeleteCertificate(long id, long certificateId)
    {
        if (!await CanReviewAsync()) return Forbidden();
        if (!await LineVisibleAsync(id)) return NotFound();
        return await _service.DeleteCertificateAsync(id, certificateId) ? Ok() : NotFound();
    }

    /// <summary>Accepted lines: switch auto-fill onto new RFQs on or off.</summary>
    [HttpPatch("{id:long}/auto-apply")]
    [Auditable("PendingSupplierQuote", "AutoApply", CaptureBody = true)]
    public async Task<IActionResult> SetAutoApply(long id, [FromBody] SetAutoApplyRequest request)
    {
        if (!await CanReviewAsync()) return Forbidden();
        if (!await LineVisibleAsync(id)) return NotFound();
        return await _service.SetAutoApplyAsync(id, request.Enabled) ? Ok() : NotFound();
    }

    [HttpPost("accept")]
    [Auditable("PendingSupplierQuote", "Accept", CaptureBody = true)]
    public async Task<ActionResult<AcceptPendingQuotesResponse>> Accept([FromBody] PendingQuoteIdsRequest request)
    {
        if (!await CanReviewAsync()) return Forbidden();
        var ids = await VisibleIdsAsync(request.Ids);
        var pending = await _db.Set<PendingSupplierQuote>().AsNoTracking()
            .Where(p => ids.Contains(p.Id) && p.Status == "Pending")
            .Select(p => p.Id)
            .ToListAsync();
        var result = await _service.AcceptAsync(ids, GetUserId(), addSenderEmails: await HasFeatureAsync(SupplierEmailsFeature));
        // The suppliers' lines under Procurement Plus parts take these prices (and go onto the parts' RFQs).
        await _ppe.PriceFromAcceptedEmailsAsync(pending, GetUserId());
        return Ok(result);
    }

    /// <summary>Accepted lines: write their current values to their RFQs again (rewrite or add).</summary>
    [HttpPost("reapply")]
    [Auditable("PendingSupplierQuote", "Reapply", CaptureBody = true)]
    public async Task<ActionResult<ReapplyPendingQuotesResponse>> Reapply([FromBody] PendingQuoteIdsRequest request)
    {
        if (!await CanReviewAsync()) return Forbidden();
        return Ok(await _service.ReapplyAsync(await VisibleIdsAsync(request.Ids), GetUserId()));
    }

    [HttpPost("remove")]
    [Auditable("PendingSupplierQuote", "Remove", CaptureBody = true)]
    public async Task<IActionResult> Remove([FromBody] PendingQuoteIdsRequest request)
    {
        if (!await CanReviewAsync()) return Forbidden();
        return Ok(new { removed = await _service.RemoveAsync(await VisibleIdsAsync(request.Ids), GetUserId()) });
    }

    /// <summary>Every pending line on every page (header "Remove all").</summary>
    [HttpPost("remove-all")]
    [Auditable("PendingSupplierQuote", "RemoveAll")]
    public async Task<IActionResult> RemoveAll()
    {
        if (!await CanReviewAsync()) return Forbidden();
        return Ok(new { removed = await _service.RemoveAllPendingAsync(GetUserId(), await AllowedMailboxesAsync()) });
    }

    /// <summary>Removed lines with who removed them; ?days=30, ?userId=, ?search= to narrow; paged by email.</summary>
    [HttpGet("removed")]
    public async Task<ActionResult<PendingQuotePageResponse>> GetRemoved(
        [FromQuery] int? days, [FromQuery] long? userId, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? mailbox = null)
    {
        if (!await CanReviewAsync()) return Forbidden();
        return Ok(await _service.GetRemovedPageAsync(days, userId, search, page, pageSize, mailbox, await AllowedMailboxesAsync()));
    }

    /// <summary>Put removed lines back into Pending so they can be accepted after all.</summary>
    [HttpPost("restore")]
    [Auditable("PendingSupplierQuote", "Restore", CaptureBody = true)]
    public async Task<IActionResult> Restore([FromBody] PendingQuoteIdsRequest request)
    {
        if (!await CanReviewAsync()) return Forbidden();
        return Ok(new { restored = await _service.RestoreAsync(await VisibleIdsAsync(request.Ids)) });
    }

    /// <summary>The source email: headers, body and attachment list.</summary>
    [HttpGet("emails/{emailId:long}")]
    public async Task<ActionResult<PendingQuoteEmailResponse>> GetEmail(long emailId)
    {
        if (!await CanReviewAsync()) return Forbidden();
        if (!await _service.EmailVisibleAsync(emailId, await AllowedMailboxesAsync())) return NotFound();
        var email = await _service.GetEmailAsync(emailId);
        return email == null ? NotFound() : Ok(email);
    }

    /// <summary>One attachment of the source email (opened inline, e.g. a PDF preview).</summary>
    [HttpGet("emails/{emailId:long}/attachments/{attachmentId:long}")]
    public async Task<IActionResult> GetAttachment(long emailId, long attachmentId)
    {
        if (!await CanReviewAsync()) return Forbidden();
        if (!await _service.EmailVisibleAsync(emailId, await AllowedMailboxesAsync())) return NotFound();
        var file = await _service.GetAttachmentAsync(emailId, attachmentId);
        if (file == null) return NotFound();
        var (stream, fileName, contentType) = file.Value;
        // No download name: served inline so the viewer can preview it; the client sets the save name.
        Response.Headers["X-File-Name"] = Uri.EscapeDataString(fileName);
        return File(stream, contentType);
    }

    /// <summary>Approved catalog suppliers for the supplier picker (the /suppliers API is role-limited).</summary>
    [HttpGet("suppliers")]
    public async Task<ActionResult<List<SupplierLookupItem>>> Suppliers([FromQuery] string? q)
    {
        if (!await CanReviewAsync()) return Forbidden();
        return Ok(await _service.SearchSuppliersAsync(q));
    }
}
