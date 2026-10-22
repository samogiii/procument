using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Identity.Entities;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Services;
using Procument.Shared.Audit;

namespace Procument.Module.Purchasing.Controllers;

/// <summary>
/// Procurement Plus page: pick parts from RFQs, upload availability files, see which (preferred)
/// suppliers list them, source them and mark the prices Done onto RFQs. Granted per user in
/// Menu Access ("procurementPlus"); SuperAdmin always passes.
/// </summary>
[ApiController]
[Route("api/procurement-plus")]
[Authorize]
public class ProcurementPlusController : ControllerBase
{
    public const string Feature = "procurementPlus";

    private readonly IProcurementPlusService _service;
    private readonly IPendingSupplierQuoteService _quotes;
    private readonly DbContext _db;

    public ProcurementPlusController(IProcurementPlusService service, IPendingSupplierQuoteService quotes, DbContext db)
    {
        _service = service;
        _quotes = quotes;
        _db = db;
    }

    private async Task<bool> HasAccessAsync()
    {
        if (User.IsInRole(UserRoles.SuperAdmin)) return true;
        var userName = User.Identity?.Name;
        return !string.IsNullOrEmpty(userName) && await _db.Set<MenuPermission>()
            .AnyAsync(p => p.Feature == Feature && p.UserName == userName);
    }

    private ObjectResult Forbidden() =>
        StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not have access to Procurement Plus." });

    private long UserId =>
        long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

    /// <summary>
    /// Whose page: every user has their own. SuperAdmin can open another user's (?asUser=id) or every
    /// user's at once (?asUser=all → null) to see who works on which part.
    /// </summary>
    private long? ViewUserId()
    {
        if (User.IsInRole(UserRoles.SuperAdmin))
        {
            var q = Request.Query["asUser"].ToString();
            if (q == "all") return null;
            if (long.TryParse(q, out var id) && id > 0) return id;
        }
        return UserId;
    }

    /// <summary>One user's page (files, availability, sourcing): the viewed user, or me in the "All users" view.</summary>
    private long PageUserId => ViewUserId() ?? UserId;

    private PpUserScope Scope()
    {
        var bases = (User.FindFirst("bases")?.Value ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var b) ? b : -1)
            .Where(b => b > 0)
            .ToArray();
        return new PpUserScope(PageUserId, User.IsInRole(UserRoles.SuperAdmin), bases);
    }

    private async Task<ActionResult> Run<T>(Func<Task<T>> action)
    {
        if (!await HasAccessAsync()) return Forbidden();
        try
        {
            return Ok(await action());
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ──── Parts ────

    /// <summary>RFQs to pick parts from (no search = deadline at most a week ago).</summary>
    [HttpGet("rfqs")]
    public Task<ActionResult> SearchRfqs([FromQuery] string? search) => Run(() => _service.SearchRfqsAsync(Scope(), search));

    [HttpGet("rfqs/{rfqId:long}/items")]
    public Task<ActionResult> RfqItems(long rfqId) => Run(() => _service.GetRfqItemsAsync(Scope(), rfqId));

    /// <summary>RFQ items whose part number contains the search.</summary>
    [HttpGet("rfq-items")]
    public Task<ActionResult> SearchRfqItems([FromQuery] string? search) => Run(() => _service.SearchRfqItemsAsync(Scope(), search));

    [HttpGet("parts")]
    public Task<ActionResult> Parts([FromQuery] bool includeHidden = false, [FromQuery] string? track = null) =>
        Run(() => _service.GetPartsAsync(ViewUserId(), includeHidden, track));

    [HttpPost("parts")]
    [Auditable("ProcurementPlus", "AddParts", CaptureBody = true)]
    public Task<ActionResult> AddParts([FromBody] PpAddPartsRequest request) => Run(() => _service.AddPartsAsync(Scope(), request));

    /// <summary>Edit a preferred part's description / alternatives (stored on the preferred, not in the catalog).</summary>
    [HttpPatch("parts/{id:long}")]
    [Auditable("ProcurementPlus", "UpdatePart", CaptureBody = true)]
    public async Task<ActionResult> UpdatePart(long id, [FromBody] PpUpdatePartRequest request)
    {
        if (!await HasAccessAsync()) return Forbidden();
        try
        {
            var part = await _service.UpdatePartAsync(ViewUserId(), UserId, id, request);
            return part == null ? NotFound() : Ok(part);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Move parts to ILS / Project / WeChat (e.g. "No supplier" parts → Project), with a target price each.</summary>
    [HttpPost("parts/track")]
    [Auditable("ProcurementPlus", "SetTrack", CaptureBody = true)]
    public Task<ActionResult> SetTrack([FromBody] PpSetTrackRequest request) =>
        Run(async () => new { updated = await _service.SetTrackAsync(ViewUserId(), UserId, request) });

    /// <summary>
    /// Parts (track=All: every track + saved-RFQ items not on the list) / ILS / Project / WeChat board, one page: parts with
    /// their supplier lines, file rows, price stats and timeline. ?origin=parts|project|wechat|rfq (Parts tab),
    /// ?status=all|none|sourcing|ready|done|nofiles, ?sort=added|pn|deadline|suppliers, ?search=, ?page=, ?pageSize= (max 100).
    /// </summary>
    [HttpGet("board")]
    public Task<ActionResult> Board(
        [FromQuery] string track = "ILS", [FromQuery] bool includeHidden = false, [FromQuery] string? search = null,
        [FromQuery] string? status = null, [FromQuery] string? origin = null, [FromQuery] string? sort = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25) =>
        Run(() => _service.GetBoardAsync(ViewUserId(), track, includeHidden, search, status, origin, sort, page, pageSize));

    /// <summary>Every part number on the board with these filters, not paged — "Copy all" on the Parts tab (track=All).</summary>
    [HttpGet("board/part-numbers")]
    public Task<ActionResult> BoardPartNumbers(
        [FromQuery] string track = "All", [FromQuery] bool includeHidden = false, [FromQuery] string? search = null,
        [FromQuery] string? status = null, [FromQuery] string? origin = null, [FromQuery] string? sort = null) =>
        Run(() => _service.GetBoardPartNumbersAsync(ViewUserId(), track, includeHidden, search, status, origin, sort));

    /// <summary>"By supplier": suppliers with lines on this board's parts, paged.</summary>
    [HttpGet("board/suppliers")]
    public Task<ActionResult> BoardSuppliers(
        [FromQuery] string track = "ILS", [FromQuery] string? search = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 25) =>
        Run(() => _service.GetSupplierViewAsync(ViewUserId(), track, search, page, pageSize));

    /// <summary>My saved RFQs (By RFQ tab) — kept per user so they are still there after a refresh.</summary>
    [HttpGet("my-rfqs")]
    public Task<ActionResult> MyRfqs() => Run(() => _service.GetMyRfqsAsync(Scope(), PageUserId));

    [HttpPost("my-rfqs")]
    public async Task<ActionResult> SaveMyRfq([FromBody] PpRfqIdRequest request)
    {
        if (!await HasAccessAsync()) return Forbidden();
        return await _service.SaveMyRfqAsync(Scope(), PageUserId, request.RfqId) ? Ok() : NotFound();
    }

    [HttpDelete("my-rfqs/{rfqId:long}")]
    public async Task<ActionResult> RemoveMyRfq(long rfqId)
    {
        if (!await HasAccessAsync()) return Forbidden();
        return await _service.RemoveMyRfqAsync(PageUserId, rfqId) ? Ok() : NotFound();
    }

    /// <summary>"By RFQ": every item of the RFQ — quotes already on it, and the part on my list with its lines.</summary>
    [HttpGet("rfqs/{rfqId:long}/coverage")]
    public async Task<ActionResult> RfqCoverage(long rfqId)
    {
        if (!await HasAccessAsync()) return Forbidden();
        var coverage = await _service.GetRfqCoverageAsync(Scope(), ViewUserId(), rfqId);
        return coverage == null ? NotFound() : Ok(coverage);
    }

    /// <summary>A part's history (clock button): supplier quotes for it on RFQs + its timeline. Negative id = a saved-RFQ item not on the list.</summary>
    [HttpGet("parts/{id:long}/history")]
    public async Task<ActionResult> PartHistory(long id)
    {
        if (!await HasAccessAsync()) return Forbidden();
        var history = await _service.GetPartHistoryAsync(ViewUserId(), id);
        return history == null ? NotFound() : Ok(history);
    }

    /// <summary>Add a supplier under a part (preferred, catalog or a typed name).</summary>
    [HttpPost("parts/{id:long}/lines")]
    [Auditable("ProcurementPlus", "AddLine", CaptureBody = true)]
    public Task<ActionResult> AddLine(long id, [FromBody] PpAddLineRequest request) =>
        Run(() => _service.AddLineAsync(ViewUserId(), UserId, id, request));

    /// <summary>A note on the part's tracing timeline.</summary>
    [HttpPost("parts/{id:long}/notes")]
    public Task<ActionResult> AddNote(long id, [FromBody] PpNoteRequest request) =>
        Run(() => _service.AddNoteAsync(ViewUserId(), UserId, id, request.Text));

    /// <summary>SuperAdmin: everyone with a Procurement Plus page (user filter).</summary>
    [HttpGet("users")]
    public async Task<ActionResult> Users()
    {
        if (!User.IsInRole(UserRoles.SuperAdmin)) return Forbidden();
        return Ok(await _service.GetUsersAsync());
    }

    [HttpPost("parts/hide")]
    public Task<ActionResult> HideParts([FromBody] PpIdsRequest request) =>
        Run(async () => new { updated = await _service.SetHiddenAsync(ViewUserId(), request.Ids, true) });

    [HttpPost("parts/show")]
    public Task<ActionResult> ShowParts([FromBody] PpIdsRequest request) =>
        Run(async () => new { updated = await _service.SetHiddenAsync(ViewUserId(), request.Ids, false) });

    [HttpPost("parts/delete")]
    [Auditable("ProcurementPlus", "DeleteParts", CaptureBody = true)]
    public Task<ActionResult> DeleteParts([FromBody] PpIdsRequest request) =>
        Run(async () => new { deleted = await _service.DeletePartsAsync(ViewUserId(), request.Ids) });

    // ──── Files ────

    /// <summary>Availability files (CSV; the page converts Excel to CSV before sending). Several at once.</summary>
    [HttpPost("uploads")]
    [RequestSizeLimit(50_000_000)]
    public Task<ActionResult> Upload([FromForm] List<IFormFile> files) => Run(() => _service.UploadAsync(PageUserId, files));

    [HttpDelete("uploads/{id:long}")]
    public async Task<IActionResult> DeleteUpload(long id)
    {
        if (!await HasAccessAsync()) return Forbidden();
        return await _service.DeleteUploadAsync(PageUserId, id) ? Ok() : NotFound();
    }

    /// <summary>Removes the current (last FreshDays) files; older ones stay for History.</summary>
    [HttpDelete("uploads")]
    public Task<ActionResult> ClearUploads() => Run(async () => new { deleted = await _service.ClearUploadsAsync(PageUserId) });

    /// <summary>The Suppliers Available modal: preferred / other suppliers, parts nobody lists, history.</summary>
    [HttpGet("availability")]
    public Task<ActionResult> Availability() => Run(() => _service.GetAvailabilityAsync(PageUserId));

    /// <summary>
    /// Link a company name from the files to a catalog supplier (remembered for future files); its email
    /// addresses in the files are added to the supplier's contacts.
    /// </summary>
    [HttpPost("companies/resolve")]
    [Auditable("ProcurementPlus", "ResolveCompany", CaptureBody = true)]
    public Task<ActionResult> ResolveCompany([FromBody] PpResolveCompanyRequest request) =>
        Run(() => _service.ResolveCompanyAsync(UserId, request));

    // ──── Preferred suppliers ────

    [HttpGet("preferred-suppliers")]
    public Task<ActionResult> Preferred() => Run(() => _service.GetPreferredAsync());

    /// <summary>Add a preferred supplier — a name is enough, it does not have to be in the catalog.</summary>
    [HttpPost("preferred-suppliers")]
    [Auditable("Preferred", "AddSupplier", CaptureBody = true)]
    public Task<ActionResult> AddPreferred([FromBody] PpSavePreferredSupplierRequest request) =>
        Run(() => _service.SavePreferredAsync(UserId, null, request));

    /// <summary>Edit a preferred supplier (name, emails, phone, method, note, catalog link — supplierId 0 unlinks).</summary>
    [HttpPut("preferred-suppliers/{id:long}")]
    [Auditable("Preferred", "EditSupplier", CaptureBody = true)]
    public Task<ActionResult> EditPreferred(long id, [FromBody] PpSavePreferredSupplierRequest request) =>
        Run(() => _service.SavePreferredAsync(UserId, id, request));

    [HttpPatch("preferred-suppliers/{id:long}")]
    [Auditable("Preferred", "UpdateSupplier", CaptureBody = true)]
    public async Task<IActionResult> UpdatePreferred(long id, [FromBody] PpUpdatePreferredRequest request)
    {
        if (!await HasAccessAsync()) return Forbidden();
        return await _service.UpdatePreferredAsync(id, request) ? Ok() : NotFound();
    }

    [HttpDelete("preferred-suppliers/{id:long}")]
    [Auditable("Preferred", "RemoveSupplier")]
    public async Task<IActionResult> RemovePreferred(long id)
    {
        if (!await HasAccessAsync()) return Forbidden();
        return await _service.RemovePreferredAsync(id) ? Ok() : NotFound();
    }

    /// <summary>Link a preferred supplier to a catalog supplier — needed only to put its prices on RFQs (Done).</summary>
    [HttpPost("preferred-suppliers/{id:long}/link")]
    [Auditable("Preferred", "LinkSupplier", CaptureBody = true)]
    public async Task<ActionResult> LinkPreferred(long id, [FromBody] PpLinkPreferredRequest request)
    {
        if (!await HasAccessAsync()) return Forbidden();
        try
        {
            var result = await _service.LinkPreferredAsync(id, request);
            return result == null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Catalog suppliers for the pickers on this page (the /suppliers API is role-limited).</summary>
    [HttpGet("suppliers")]
    public Task<ActionResult> Suppliers([FromQuery] string? q) => Run(() => _service.SearchSuppliersAsync(q));

    // ──── Sourcing → Done ────

    /// <summary>Creates the lines to price for the picked suppliers (one per part and condition).</summary>
    [HttpPost("sourcing")]
    [Auditable("ProcurementPlus", "StartSourcing", CaptureBody = true)]
    public Task<ActionResult> StartSourcing([FromBody] PpStartSourcingRequest request) =>
        Run(() => _service.StartSourcingAsync(PageUserId, request));

    /// <summary>Lines being sourced — mine, or ?everyone=true.</summary>
    [HttpGet("sourcing")]
    public Task<ActionResult> Sourcing([FromQuery] bool everyone = false) => Run(() => _service.GetSourcingAsync(PageUserId, everyone));

    /// <summary>Autosave of one line (same body as an email quote line).</summary>
    [HttpPut("quotes/{id:long}")]
    [Auditable("ProcurementPlus", "UpdateLine", CaptureBody = true)]
    public async Task<ActionResult> UpdateLine(long id, [FromBody] UpdatePendingSupplierQuoteRequest request)
    {
        if (!await HasAccessAsync()) return Forbidden();
        try
        {
            var line = await _service.UpdateLineAsync(id, request);
            // The shared edit form reads the email-quote line shape.
            return line == null ? NotFound() : Ok(line.Line);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>How the line is searched: ILS / Email / Search (one or more).</summary>
    [HttpPut("quotes/{id:long}/methods")]
    [Auditable("ProcurementPlus", "LineMethods", CaptureBody = true)]
    public async Task<ActionResult> SetLineMethods(long id, [FromBody] PpLineMethodsRequest request)
    {
        if (!await HasAccessAsync()) return Forbidden();
        try
        {
            var line = await _service.SetLineMethodsAsync(id, request.Methods);
            return line == null ? NotFound() : Ok(line);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("quotes/remove")]
    [Auditable("ProcurementPlus", "RemoveLines", CaptureBody = true)]
    public Task<ActionResult> RemoveLines([FromBody] PpIdsRequest request) =>
        Run(async () => new { removed = await _service.RemoveLinesAsync(UserId, request.Ids) });

    /// <summary>Before Done: part numbers of these lines that are not in the database (custom parts) — the page warns.</summary>
    [HttpPost("quotes/done-check")]
    public Task<ActionResult> DoneCheck([FromBody] PpIdsRequest request) =>
        Run(async () => new { missing = await _service.PartsNotInDatabaseAsync(request.Ids) });

    /// <summary>Done: the priced lines go onto RFQs (deadline at most a week ago) and auto-fill new RFQs for a week.</summary>
    [HttpPost("quotes/done")]
    [Auditable("ProcurementPlus", "Done", CaptureBody = true)]
    public Task<ActionResult> Done([FromBody] PpIdsRequest request) => Run(() => _service.DoneAsync(UserId, request.Ids));

    /// <summary>Done lines: ?days=7|30|90 (0 = all), ?userId=, ?search=.</summary>
    [HttpGet("done")]
    public Task<ActionResult> DoneList([FromQuery] int? days, [FromQuery] long? userId, [FromQuery] string? search) =>
        Run(() => _service.GetDoneAsync(days, userId, search));

    [HttpPatch("quotes/{id:long}/auto-apply")]
    [Auditable("ProcurementPlus", "AutoApply", CaptureBody = true)]
    public async Task<IActionResult> SetAutoApply(long id, [FromBody] SetAutoApplyRequest request)
    {
        if (!await HasAccessAsync()) return Forbidden();
        if (!await _service.IsLineAsync(id)) return NotFound();
        return await _quotes.SetAutoApplyAsync(id, request.Enabled) ? Ok() : NotFound();
    }

    // Cert PDFs of a line — same storage as email quotes, copied to each RFQ quote on Done.

    [HttpPost("quotes/{id:long}/certificates")]
    [RequestSizeLimit(100_000_000)]
    public async Task<ActionResult> UploadCertificates(long id, [FromForm] List<IFormFile> files)
    {
        if (!await HasAccessAsync()) return Forbidden();
        if (!await _service.IsLineAsync(id)) return NotFound();
        try
        {
            var result = await _quotes.UploadCertificatesAsync(id, UserId, files);
            return result == null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("quotes/{id:long}/certificates/{certificateId:long}")]
    public async Task<IActionResult> GetCertificate(long id, long certificateId)
    {
        if (!await HasAccessAsync()) return Forbidden();
        if (!await _service.IsLineAsync(id)) return NotFound();
        var file = await _quotes.GetCertificateAsync(id, certificateId);
        if (file == null) return NotFound();
        var (stream, fileName) = file.Value;
        Response.Headers["X-File-Name"] = Uri.EscapeDataString(fileName);
        return File(stream, "application/pdf");
    }

    [HttpDelete("quotes/{id:long}/certificates/{certificateId:long}")]
    public async Task<IActionResult> DeleteCertificate(long id, long certificateId)
    {
        if (!await HasAccessAsync()) return Forbidden();
        if (!await _service.IsLineAsync(id)) return NotFound();
        return await _quotes.DeleteCertificateAsync(id, certificateId) ? Ok() : NotFound();
    }
}
