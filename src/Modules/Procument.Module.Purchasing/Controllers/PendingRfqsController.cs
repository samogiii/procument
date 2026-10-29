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
using Procument.Module.Purchasing.Services;
using Procument.Shared.Audit;

namespace Procument.Module.Purchasing.Controllers;

/// <summary>
/// Customer RFQs extracted from email by MailFlow's CSV Export, reviewed on the Email RFQs page; Accept
/// creates new RFQs. Import is for MailFlow — the permanent X-Api-Key header (MailFlow:ApiKey) or an
/// Admin/SuperAdmin token, same as Email Quotes. Review is limited to users granted the "pendingRfqs"
/// feature in Menu Access — SuperAdmin does NOT bypass (same rule as Email Quotes).
/// </summary>
[ApiController]
[Route("api/pending-rfqs")]
[Authorize]
public class PendingRfqsController : ControllerBase
{
    public const string Feature = "pendingRfqs";

    private readonly IPendingRfqService _service;
    private readonly DbContext _db;
    private readonly IConfiguration _config;

    public PendingRfqsController(IPendingRfqService service, DbContext db, IConfiguration config)
    {
        _service = service;
        _db = db;
        _config = config;
    }

    private bool HasMailFlowApiKey()
    {
        var expected = _config["MailFlow:ApiKey"];
        var provided = Request.Headers["X-Api-Key"].ToString();
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(provided)) return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(provided));
    }

    private bool IsMailFlow() => HasMailFlowApiKey() || User.IsInRole("Admin") || User.IsInRole("SuperAdmin");

    private UnauthorizedObjectResult MailFlowUnauthorized() =>
        Unauthorized(new { message = "Missing or wrong X-Api-Key (MailFlow:ApiKey in appsettings.json)." });

    private async Task<bool> CanReviewAsync()
    {
        var userName = User.Identity?.Name;
        return !string.IsNullOrEmpty(userName) && await _db.Set<MenuPermission>()
            .AnyAsync(p => p.Feature == Feature && p.UserName == userName);
    }

    private ObjectResult Forbidden() =>
        StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not have access to email RFQs." });

    private long GetUserId() =>
        long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

    /// <summary>MailFlow posts one RFQ email at a time; an already-imported Message-ID is skipped.</summary>
    [HttpPost("import")]
    [AllowAnonymous]
    [RequestSizeLimit(100_000_000)] // the email's attachments travel base64-encoded in the body
    public async Task<ActionResult<ImportPendingRfqsResponse>> Import([FromBody] ImportPendingRfqsRequest request)
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

    /// <summary>Pending lines, one page of whole emails; ?search= narrows by P/N, RFQ name, customer or subject.</summary>
    [HttpGet]
    public async Task<ActionResult<PendingRfqPageResponse>> GetPending(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? mailbox = null)
    {
        if (!await CanReviewAsync()) return Forbidden();
        return Ok(await _service.GetPendingPageAsync(search, page, pageSize, mailbox));
    }

    /// <summary>The MailFlow mailboxes lines came in through (mailbox dropdown; "-" = unknown).</summary>
    [HttpGet("mailboxes")]
    public async Task<ActionResult<List<MailboxOption>>> Mailboxes()
    {
        if (!await CanReviewAsync()) return Forbidden();
        return Ok(await _service.GetMailboxesAsync());
    }

    /// <summary>Pending + accepted lines of emails sent in [from, to) — the page builds the CSV from them.</summary>
    [HttpGet("export")]
    public async Task<ActionResult<List<PendingRfqItemResponse>>> Export(
        [FromQuery] DateTime from, [FromQuery] DateTime to, [FromQuery] string? mailbox = null)
    {
        if (!await CanReviewAsync()) return Forbidden();
        // The model binder turns "…Z" into server-local time; the email dates are stored in UTC.
        var start = from.ToUniversalTime();
        var end = to.ToUniversalTime();
        if (end <= start) return BadRequest(new { message = "The 'to' date must be after the 'from' date." });
        return Ok(await _service.GetExportAsync(start, end, mailbox));
    }

    [HttpGet("accepted")]
    public async Task<ActionResult<PendingRfqPageResponse>> GetAccepted(
        [FromQuery] int? days, [FromQuery] long? userId, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? mailbox = null)
    {
        if (!await CanReviewAsync()) return Forbidden();
        return Ok(await _service.GetAcceptedPageAsync(days, userId, search, page, pageSize, mailbox));
    }

    [HttpGet("removed")]
    public async Task<ActionResult<PendingRfqPageResponse>> GetRemoved(
        [FromQuery] int? days, [FromQuery] long? userId, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? mailbox = null)
    {
        if (!await CanReviewAsync()) return Forbidden();
        return Ok(await _service.GetRemovedPageAsync(days, userId, search, page, pageSize, mailbox));
    }

    [HttpPut("{id:long}")]
    [Auditable("PendingRfqItem", "Update", CaptureBody = true)]
    public async Task<ActionResult<PendingRfqItemResponse>> Update(long id, [FromBody] UpdatePendingRfqItemRequest request)
    {
        if (!await CanReviewAsync()) return Forbidden();
        try
        {
            var row = await _service.UpdateAsync(id, request);
            return row == null ? NotFound() : Ok(row);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>RFQ name / deadline for a whole email's pending lines.</summary>
    [HttpPost("email")]
    [Auditable("PendingRfqItem", "UpdateEmail", CaptureBody = true)]
    public async Task<IActionResult> UpdateEmail([FromBody] UpdatePendingRfqEmailRequest request)
    {
        if (!await CanReviewAsync()) return Forbidden();
        return Ok(new { updated = await _service.UpdateEmailAsync(request) });
    }

    /// <summary>Customer for a whole email's lines; the sender's address joins that customer's contacts.</summary>
    [HttpPost("customer")]
    [Auditable("PendingRfqItem", "SetCustomer", CaptureBody = true)]
    public async Task<ActionResult<SetPendingCustomerResponse>> SetCustomer([FromBody] SetPendingCustomerRequest request)
    {
        if (!await CanReviewAsync()) return Forbidden();
        try
        {
            return Ok(await _service.SetCustomerAsync(request.Ids, request.CustomerId));
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Creates the RFQs (owner = the accepting user).</summary>
    [HttpPost("accept")]
    [Auditable("PendingRfqItem", "Accept", CaptureBody = true)]
    public async Task<ActionResult<AcceptPendingRfqsResponse>> Accept([FromBody] PendingQuoteIdsRequest request)
    {
        if (!await CanReviewAsync()) return Forbidden();
        return Ok(await _service.AcceptAsync(request.Ids, GetUserId()));
    }

    [HttpPost("remove")]
    [Auditable("PendingRfqItem", "Remove", CaptureBody = true)]
    public async Task<IActionResult> Remove([FromBody] PendingQuoteIdsRequest request)
    {
        if (!await CanReviewAsync()) return Forbidden();
        return Ok(new { removed = await _service.RemoveAsync(request.Ids, GetUserId()) });
    }

    [HttpPost("remove-all")]
    [Auditable("PendingRfqItem", "RemoveAll")]
    public async Task<IActionResult> RemoveAll()
    {
        if (!await CanReviewAsync()) return Forbidden();
        return Ok(new { removed = await _service.RemoveAllPendingAsync(GetUserId()) });
    }

    [HttpPost("restore")]
    [Auditable("PendingRfqItem", "Restore", CaptureBody = true)]
    public async Task<IActionResult> Restore([FromBody] PendingQuoteIdsRequest request)
    {
        if (!await CanReviewAsync()) return Forbidden();
        return Ok(new { restored = await _service.RestoreAsync(request.Ids) });
    }

    /// <summary>Accepted tab: assign the lines' parts to a PPE (every accepted line with those P/Ns); null unassigns.</summary>
    [HttpPost("ppe")]
    [Auditable("PendingRfqItem", "AssignPpe", CaptureBody = true)]
    public async Task<ActionResult<AssignPpeResponse>> AssignPpe([FromBody] AssignPpeRequest request)
    {
        if (!await CanReviewAsync()) return Forbidden();
        try
        {
            return Ok(await _service.AssignPpeAsync(request.Ids, request.UserId, GetUserId()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Users a part can be assigned to: everyone granted Procurement Plus in Menu Access.</summary>
    [HttpGet("ppe-users")]
    public async Task<ActionResult<List<QuoteReviewerOption>>> PpeUsers()
    {
        if (!await CanReviewAsync()) return Forbidden();
        return Ok(await _service.GetPpeUsersAsync());
    }

    /// <summary>
    /// Avg price / customers / known PPE for a page's P/Ns — the tabs load their list first and fill these in after.
    /// POST only because the P/N list can be long; it changes nothing.
    /// </summary>
    [HttpPost("part-insights")]
    public async Task<ActionResult<List<PartInsight>>> PartInsights([FromBody] PartInsightsRequest request)
    {
        if (!await CanReviewAsync()) return Forbidden();
        return Ok(await _service.GetPartInsightsAsync(request));
    }

    /// <summary>Pending tab info icon: the last 25 customers who asked for the part, with what we quoted them.</summary>
    [HttpGet("part-customers")]
    public async Task<ActionResult<PartCustomersResponse>> PartCustomers([FromQuery] string partNumber)
    {
        if (!await CanReviewAsync()) return Forbidden();
        if (string.IsNullOrWhiteSpace(partNumber)) return BadRequest(new { message = "Part number is required." });
        return Ok(await _service.GetPartCustomersAsync(partNumber));
    }

    /// <summary>Active customers for the customer picker (name, code or email).</summary>
    [HttpGet("customers")]
    public async Task<ActionResult<List<CustomerLookupItem>>> Customers([FromQuery] string? q)
    {
        if (!await CanReviewAsync()) return Forbidden();
        return Ok(await _service.SearchCustomersAsync(q));
    }

    [HttpGet("emails/{emailId:long}")]
    public async Task<ActionResult<PendingQuoteEmailResponse>> GetEmail(long emailId)
    {
        if (!await CanReviewAsync()) return Forbidden();
        var email = await _service.GetEmailAsync(emailId);
        return email == null ? NotFound() : Ok(email);
    }

    [HttpGet("emails/{emailId:long}/attachments/{attachmentId:long}")]
    public async Task<IActionResult> GetAttachment(long emailId, long attachmentId)
    {
        if (!await CanReviewAsync()) return Forbidden();
        var file = await _service.GetAttachmentAsync(emailId, attachmentId);
        if (file == null) return NotFound();
        var (stream, fileName, contentType) = file.Value;
        Response.Headers["X-File-Name"] = Uri.EscapeDataString(fileName);
        return File(stream, contentType);
    }
}
