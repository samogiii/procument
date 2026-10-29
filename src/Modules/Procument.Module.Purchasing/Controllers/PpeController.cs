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
/// The new Procurement Plus page: parts assigned to the user (the PPE) from Email RFQs, each across every RFQ
/// asking for it (deadline at most 14 days ago), plus the ILS files tab. Granted per user in Menu Access
/// ("procurementPlus"); SuperAdmin always passes and can open another user's page (?asUser=id).
/// </summary>
[ApiController]
[Route("api/ppe")]
[Authorize]
public class PpeController : ControllerBase
{
    public const string Feature = "procurementPlus";

    private readonly IPpeService _service;
    private readonly DbContext _db;

    public PpeController(IPpeService service, DbContext db)
    {
        _service = service;
        _db = db;
    }

    private bool IsSuperAdmin => User.IsInRole(UserRoles.SuperAdmin);

    private async Task<bool> HasAccessAsync()
    {
        if (IsSuperAdmin) return true;
        var userName = User.Identity?.Name;
        return !string.IsNullOrEmpty(userName) && await _db.Set<MenuPermission>()
            .AnyAsync(p => p.Feature == Feature && p.UserName == userName);
    }

    private ObjectResult Forbidden() =>
        StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not have access to Procurement Plus." });

    private long UserId =>
        long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

    /// <summary>Whose page: mine, or (SuperAdmin) another user's via ?asUser=id.</summary>
    private long PageUserId
    {
        get
        {
            if (IsSuperAdmin && long.TryParse(Request.Query["asUser"].ToString(), out var id) && id > 0) return id;
            return UserId;
        }
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

    /// <summary>folder = one of my folders; none = the public folder (every part).</summary>
    [HttpGet("parts")]
    public Task<ActionResult> Parts([FromQuery] string? search, [FromQuery] bool showOld = false, [FromQuery] long? folder = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        Run(() => _service.GetPartsAsync(PageUserId, search, showOld, folder, page, pageSize));

    /// <summary>"Copy all P/N": every part number on the Parts tab with the same filters (no paging).</summary>
    [HttpGet("parts/part-numbers")]
    public Task<ActionResult> PartNumbers([FromQuery] string? search, [FromQuery] bool showOld = false, [FromQuery] long? folder = null) =>
        Run(() => _service.GetPpePartNumbersAsync(PageUserId, search, showOld, folder));

    // ──── Folders (each user's own) ────

    [HttpGet("folders")]
    public Task<ActionResult> Folders() => Run(() => _service.GetFoldersAsync(PageUserId));

    [HttpPost("folders")]
    [Auditable("Ppe", "AddFolder", CaptureBody = true)]
    public Task<ActionResult> AddFolder([FromBody] PpeFolderRequest request) =>
        Run(() => _service.SaveFolderAsync(PageUserId, UserId, null, request.Name));

    [HttpPut("folders/{id:long}")]
    [Auditable("Ppe", "RenameFolder", CaptureBody = true)]
    public Task<ActionResult> RenameFolder(long id, [FromBody] PpeFolderRequest request) =>
        Run(() => _service.SaveFolderAsync(PageUserId, UserId, id, request.Name));

    /// <summary>Deletes the folder; its parts stay on the list (public folder).</summary>
    [HttpDelete("folders/{id:long}")]
    [Auditable("Ppe", "DeleteFolder")]
    public async Task<IActionResult> DeleteFolder(long id)
    {
        if (!await HasAccessAsync()) return Forbidden();
        return await _service.DeleteFolderAsync(PageUserId, id) ? Ok() : NotFound();
    }

    /// <summary>Parts into / out of my folders (a part can be in several).</summary>
    [HttpPost("parts/folders")]
    [Auditable("Ppe", "SetPartFolders", CaptureBody = true)]
    public Task<ActionResult> SetPartFolders([FromBody] PpeSetFoldersRequest request) =>
        Run(async () => new { updated = await _service.SetPartFoldersAsync(PageUserId, request) });

    [HttpGet("parts/{id:long}")]
    public async Task<ActionResult> Part(long id)
    {
        if (!await HasAccessAsync()) return Forbidden();
        var part = await _service.GetPartAsync(PageUserId, UserId, id);
        return part == null ? NotFound() : Ok(part);
    }

    /// <summary>Add part dialog: search the parts database (P/N, new name, alternatives).</summary>
    [HttpGet("catalog-parts")]
    public Task<ActionResult> CatalogParts([FromQuery] string? q) =>
        Run(() => _service.SearchCatalogPartsAsync(PageUserId, q));

    /// <summary>Add a part by hand (from the database or typed) to my list and work on it like an assigned one.</summary>
    [HttpPost("parts")]
    [Auditable("Ppe", "AddPart", CaptureBody = true)]
    public Task<ActionResult> AddPart([FromBody] PpeAddPartRequest request) =>
        Run(() => _service.AddPpePartAsync(PageUserId, UserId, request));

    /// <summary>Bulk import: pasted Part Number / Qty / Condition rows, each added like Add part.</summary>
    [HttpPost("parts/bulk")]
    [Auditable("Ppe", "AddPartsBulk", CaptureBody = true)]
    public Task<ActionResult> AddPartsBulk([FromBody] PpeBulkAddPartsRequest request) =>
        Run(() => _service.AddPpePartsAsync(PageUserId, UserId, request.Items, request.FolderIds));

    /// <summary>Remove parts from my list (they come back with "Show removed" or when assigned again).</summary>
    [HttpPost("parts/hide")]
    [Auditable("Ppe", "HideParts", CaptureBody = true)]
    public Task<ActionResult> Hide([FromBody] PpeIdsRequest request) =>
        Run(async () => new { updated = await _service.SetPartsHiddenAsync(PageUserId, request.Ids, true) });

    [HttpPost("parts/show")]
    [Auditable("Ppe", "ShowParts", CaptureBody = true)]
    public Task<ActionResult> Show([FromBody] PpeIdsRequest request) =>
        Run(async () => new { updated = await _service.SetPartsHiddenAsync(PageUserId, request.Ids, false) });

    [HttpGet("parts/{id:long}/history")]
    public async Task<ActionResult> History(long id)
    {
        if (!await HasAccessAsync()) return Forbidden();
        var history = await _service.GetPpeHistoryAsync(PageUserId, id);
        return history == null ? NotFound() : Ok(history);
    }

    /// <summary>Available suppliers for the part: rows of my current ILS files.</summary>
    [HttpGet("parts/{id:long}/offers")]
    public Task<ActionResult> Offers(long id) => Run(() => _service.GetPartOffersAsync(PageUserId, id));

    /// <summary>
    /// SupplierQuoteEditDialog save under a part: a draft without a price stays here; with a price and an approved
    /// catalog supplier it is written to every RFQ for the part (deadline at most 14 days ago).
    /// </summary>
    [HttpPost("parts/{id:long}/quotes")]
    [Auditable("Ppe", "SaveQuote", CaptureBody = true)]
    public Task<ActionResult> SaveQuote(long id, [FromBody] PpeSaveQuoteRequest request) =>
        Run(() => _service.SaveQuoteAsync(PageUserId, UserId, id, request));

    /// <summary>Add Commission: one coef per supplier per customer → Coef 1 on those RFQ quotes (locked), part leaves the list.</summary>
    [HttpPost("parts/{id:long}/commission")]
    [Auditable("Ppe", "Commission", CaptureBody = true)]
    public Task<ActionResult> Commission(long id, [FromBody] PpeCommissionRequest request) =>
        Run(() => _service.SaveCommissionAsync(PageUserId, UserId, IsSuperAdmin, id, request));

    // ──── Lines ────

    /// <summary>Picked ILS rows → draft supplier lines (no price) on the parts they answer.</summary>
    [HttpPost("lines/from-offers")]
    [Auditable("Ppe", "AddOffers", CaptureBody = true)]
    public Task<ActionResult> AddOffers([FromBody] PpeAddOffersRequest request) =>
        Run(() => _service.AddOffersAsync(PageUserId, request));

    [HttpDelete("lines/{id:long}")]
    [Auditable("Ppe", "RemoveLine")]
    public async Task<IActionResult> RemoveLine(long id)
    {
        if (!await HasAccessAsync()) return Forbidden();
        return await _service.RemoveLineAsync(PageUserId, UserId, id) ? Ok() : NotFound();
    }

    // ──── ILS files ────

    [HttpGet("ils")]
    public Task<ActionResult> Ils() => Run(() => _service.GetIlsAsync(PageUserId));

    [HttpPost("uploads")]
    [RequestSizeLimit(50_000_000)]
    public Task<ActionResult> Upload([FromForm] List<IFormFile> files) =>
        Run(() => _service.UploadPpeAsync(PageUserId, files));

    [HttpDelete("uploads/{id:long}")]
    [Auditable("Ppe", "DeleteUpload")]
    public async Task<IActionResult> DeleteUpload(long id)
    {
        if (!await HasAccessAsync()) return Forbidden();
        return await _service.DeleteUploadAsync(PageUserId, id) ? Ok() : NotFound();
    }

    // ──── Preferred suppliers (each user's own list — the same one ProMax uses) ────

    [HttpGet("preferred-suppliers")]
    public Task<ActionResult> Preferred() => Run(() => _service.GetPreferredAsync(PageUserId));

    /// <summary>Add a preferred supplier — a name is enough; adding one already on the list merges into it.</summary>
    [HttpPost("preferred-suppliers")]
    [Auditable("Preferred", "AddSupplier", CaptureBody = true)]
    public Task<ActionResult> AddPreferred([FromBody] PpSavePreferredSupplierRequest request) =>
        Run(() => _service.SavePreferredAsync(PageUserId, UserId, null, request));

    /// <summary>Edit (name, emails, phone, method, note, catalog link — supplierId 0 unlinks).</summary>
    [HttpPut("preferred-suppliers/{id:long}")]
    [Auditable("Preferred", "EditSupplier", CaptureBody = true)]
    public Task<ActionResult> EditPreferred(long id, [FromBody] PpSavePreferredSupplierRequest request) =>
        Run(() => _service.SavePreferredAsync(PageUserId, UserId, id, request));

    [HttpPatch("preferred-suppliers/{id:long}")]
    [Auditable("Preferred", "UpdateSupplier", CaptureBody = true)]
    public async Task<IActionResult> UpdatePreferred(long id, [FromBody] PpUpdatePreferredRequest request)
    {
        if (!await HasAccessAsync()) return Forbidden();
        return await _service.UpdatePreferredAsync(PageUserId, id, request) ? Ok() : NotFound();
    }

    [HttpDelete("preferred-suppliers/{id:long}")]
    [Auditable("Preferred", "RemoveSupplier")]
    public async Task<IActionResult> RemovePreferred(long id)
    {
        if (!await HasAccessAsync()) return Forbidden();
        return await _service.RemovePreferredAsync(PageUserId, id) ? Ok() : NotFound();
    }

    /// <summary>Catalog suppliers for the preferred supplier form (the /suppliers API is role-limited).</summary>
    [HttpGet("suppliers")]
    public Task<ActionResult> Suppliers([FromQuery] string? q) => Run(() => _service.SearchSuppliersAsync(PageUserId, q));

    /// <summary>SuperAdmin user filter.</summary>
    [HttpGet("users")]
    public async Task<ActionResult> Users()
    {
        if (!IsSuperAdmin) return Forbidden();
        return Ok(await _service.GetPpeUsersAsync());
    }
}
