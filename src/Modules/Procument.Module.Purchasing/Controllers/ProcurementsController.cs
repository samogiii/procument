using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Entities;
using Procument.Module.Purchasing.Services;
using Procument.Shared.Audit;
using Procument.Shared.DTOs;

namespace Procument.Module.Purchasing.Controllers;

[ApiController]
[Route("api/procurements")]
[Authorize(Roles = "Admin,SuperAdmin,Expert,Payment")]
public class ProcurementsController : ControllerBase
{
    private readonly IProcurementService _service;
    private readonly DbContext _db;

    public ProcurementsController(IProcurementService service, DbContext db)
    {
        _service = service;
        _db = db;
    }

    private (long userId, bool isAdmin, bool isSuperAdmin, int[] userBases) GetCurrentUser()
    {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        long.TryParse(userIdStr, out var userId);
        var isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
        var isSuperAdmin = User.IsInRole("SuperAdmin");
        var basesClaim = User.FindFirst("bases")?.Value ?? "";
        int[] userBases = basesClaim.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var b) ? b : -1)
            .Where(b => b > 0).ToArray();
        return (userId, isAdmin, isSuperAdmin, userBases);
    }

    /// <summary>Paged list — admin/superadmin see all, others see only Procurements assigned to them.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProcurementResponse>>> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 200, [FromQuery] string? search = null)
    {
        var pq = new PageQuery { Page = page, PageSize = pageSize, Search = search };
        var (userId, isAdmin, isSuperAdmin, userBases) = GetCurrentUser();
        var result = await _service.GetAllAsync(pq, userId, isAdmin, isSuperAdmin, userBases);
        return Ok(result);
    }

    /// <summary>Flat paged list of procurement items. Admin sees all; others see only their assigned items.</summary>
    [HttpGet("items")]
    public async Task<ActionResult<PagedResult<ProcurementItemFlatResponse>>> GetAllItems(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null,
        [FromQuery] List<string>? status = null,
        [FromQuery] List<string>? procStatus = null,
        [FromQuery] List<string>? customerName = null,
        [FromQuery] List<long>? userIds = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDesc = false,
        [FromQuery] List<string>? partNames = null,
        [FromQuery] List<string>? conditions = null,
        [FromQuery] List<string>? supplierNames = null)
    {
        var (userId, isAdmin, isSuperAdmin, userBases) = GetCurrentUser();
        var result = await _service.GetAllItemsFlatAsync(userId, isAdmin, page, pageSize, search, status, procStatus, customerName, userIds, sortBy, sortDesc, partNames, conditions, supplierNames, isSuperAdmin, userBases);
        return Ok(result);
    }

    /// <summary>Full procurement detail with items + supplier quotes + assigned users.</summary>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<ProcurementResponse>> GetById(long id)
    {
        var (userId, isAdmin, isSuperAdmin, userBases) = GetCurrentUser();
        var result = await _service.GetByIdAsync(id, userId, isAdmin);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Edit one procurement item (editable fields only — source/RFQ/Quote/Supplier snapshots are immutable).</summary>
    [HttpPatch("{id:long}/items/{itemId:long}")]
    [Auditable("Procurement", "UpdateItem", CaptureBody = true)]
    public async Task<IActionResult> UpdateItem(long id, long itemId, [FromBody] UpdateProcurementItemRequest request)
    {
        var (userId, isAdmin, isSuperAdmin, userBases) = GetCurrentUser();
        if (!await _service.UserCanAccessItemAsync(id, itemId, userId, isAdmin)) return Forbid();

        var ok = await _service.UpdateItemAsync(id, itemId, request);
        return ok ? Ok() : BadRequest(new { message = "Unable to update item (finalized/cancelled or not found)." });
    }

    /// <summary>Add a new supplier quote or update one already attached to this item.</summary>
    [HttpPost("{id:long}/items/{itemId:long}/supplier-quotes")]
    [Auditable("Procurement", "UpsertSupplierQuote", CaptureBody = true)]
    public async Task<ActionResult<ProcurementSupplierQuoteResponse>> UpsertSupplierQuote(long id, long itemId, [FromBody] UpsertSupplierQuoteRequest request)
    {
        var (userId, isAdmin, isSuperAdmin, userBases) = GetCurrentUser();
        if (!await _service.UserCanAccessItemAsync(id, itemId, userId, isAdmin)) return Forbid();

        var result = await _service.UpsertSupplierQuoteAsync(id, itemId, request, userId);
        return result == null ? BadRequest(new { message = "Unable to upsert supplier quote." }) : Ok(result);
    }

    /// <summary>Mark a supplier quote as selected — updates CurrentSupplierId / UnitPrice on the parent item.</summary>
    [HttpPost("{id:long}/items/{itemId:long}/supplier-quotes/{sqId:long}/select")]
    [Auditable("Procurement", "SelectSupplierQuote")]
    public async Task<IActionResult> SelectSupplierQuote(long id, long itemId, long sqId)
    {
        var (userId, isAdmin, isSuperAdmin, userBases) = GetCurrentUser();
        if (!await _service.UserCanAccessItemAsync(id, itemId, userId, isAdmin)) return Forbid();

        var ok = await _service.SelectSupplierQuoteAsync(id, itemId, sqId);
        return ok ? Ok() : BadRequest(new { message = "Unable to select supplier quote." });
    }

    /// <summary>Delete a supplier quote from a procurement item.</summary>
    [HttpDelete("{id:long}/items/{itemId:long}/supplier-quotes/{sqId:long}")]
    [Auditable("Procurement", "DeleteSupplierQuote")]
    public async Task<IActionResult> DeleteSupplierQuote(long id, long itemId, long sqId)
    {
        var (userId, isAdmin, isSuperAdmin, userBases) = GetCurrentUser();
        if (!await _service.UserCanAccessItemAsync(id, itemId, userId, isAdmin)) return Forbid();

        var ok = await _service.DeleteSupplierQuoteAsync(id, itemId, sqId);
        return ok ? NoContent() : BadRequest(new { message = "Unable to delete supplier quote." });
    }

    /// <summary>Finalize ALL items — materializes POItems from the edited snapshot and locks the Procurement.</summary>
    [HttpPost("{id:long}/finalize")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [Auditable("Procurement", "Finalize", CaptureBody = true)]
    public async Task<ActionResult<FinalizeProcurementResponse>> Finalize(long id, [FromBody] FinalizeProcurementRequest? request)
    {
        var (userId, _, _, _) = GetCurrentUser();
        var result = await _service.FinalizeAsync(id, userId, request);
        return result == null ? BadRequest(new { message = "Procurement not found or already finalized/cancelled." }) : Ok(result);
    }

    /// <summary>
    /// Finalize a SINGLE item (one supplier row) independently.
    /// Creates POItem(s) only for that item. The procurement status is auto-set to Finalized
    /// once all items have been individually finalized.
    /// </summary>
    [HttpPost("{id:long}/items/{itemId:long}/finalize")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [Auditable("Procurement", "FinalizeItem")]
    public async Task<ActionResult<FinalizeProcurementItemResponse>> FinalizeItem(long id, long itemId)
    {
        var (userId, _, _, _) = GetCurrentUser();
        var result = await _service.FinalizeItemAsync(id, itemId, userId);
        if (result == null)
            return BadRequest(new { message = "Item not found, or procurement is cancelled." });
        return Ok(result);
    }

    /// <summary>
    /// Admin approves a single selected supplier quote row.
    /// Creates one POItem from that quote's Qty/Price/Supplier data.
    /// The quote must already be marked IsSelected by the user.
    /// Auto-finalizes the procurement when every selected quote across all items is approved.
    /// </summary>
    [HttpPost("{id:long}/items/{itemId:long}/supplier-quotes/{sqId:long}/approve")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [Auditable("Procurement", "ApproveSupplierQuote")]
    public async Task<ActionResult<FinalizeProcurementItemResponse>> ApproveSupplierQuote(long id, long itemId, long sqId)
    {
        var (userId, _, _, _) = GetCurrentUser();
        var result = await _service.FinalizeSupplierQuoteAsync(id, itemId, sqId, userId);
        if (result == null)
            return BadRequest(new { message = "Quote not found, not selected, or procurement is cancelled." });
        return Ok(result);
    }

    /// <summary>Cancel a procurement — admin abort before finalization.</summary>
    [HttpPost("{id:long}/cancel")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [Auditable("Procurement", "Cancel")]
    public async Task<IActionResult> Cancel(long id)
    {
        var (userId, _, _, _) = GetCurrentUser();
        var ok = await _service.CancelAsync(id, userId);
        return ok ? Ok() : BadRequest(new { message = "Unable to cancel procurement." });
    }

    /// <summary>
    /// Reopen a finalized procurement so additional supplier quotes can be added to cover
    /// remaining qty. Existing approved POItems are untouched.
    /// </summary>
    [HttpPost("{id:long}/reopen")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [Auditable("Procurement", "Reopen")]
    public async Task<IActionResult> Reopen(long id)
    {
        var (userId, _, _, _) = GetCurrentUser();
        var ok = await _service.ReopenAsync(id, userId);
        return ok ? Ok() : BadRequest(new { message = "Procurement is not finalized or does not exist." });
    }

    /// <summary>
    /// Admin manually force-finalizes a procurement regardless of qty satisfaction.
    /// Useful when the admin decides the procurement is complete as-is.
    /// </summary>
    [HttpPost("{id:long}/force-finalize")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [Auditable("Procurement", "ForceFinalize")]
    public async Task<IActionResult> ForceFinalize(long id)
    {
        var (userId, _, _, _) = GetCurrentUser();
        var ok = await _service.ForceFinalizeAsync(id, userId);
        return ok ? Ok() : BadRequest(new { message = "Procurement not found or already finalized/cancelled." });
    }

    /// <summary>Distinct filter options for the Procurement Items list.</summary>
    [HttpGet("items/filter-options")]
    public async Task<ActionResult> GetItemsFilterOptions()
    {
        // Distinct item statuses from ProcurementItem
        var itemStatuses = await _db.Set<ProcurementItem>()
            .AsNoTracking()
            .Select(i => i.ItemStatus)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync();

        // Customer names: join Procurement -> Invoice -> Customer via raw SQL
        // since the Purchasing module does not reference the Sales module.
        var customerNames = await _db.Database
            .SqlQuery<string>($@"
                SELECT DISTINCT c.CustomerCode AS [Value]
                FROM Procurements p
                JOIN Invoices i ON i.Id = p.InvoiceId
                JOIN Customers c ON c.Id = i.CustomerId
                WHERE c.CustomerCode IS NOT NULL
                ORDER BY c.CustomerCode")
            .ToListAsync();

        return Ok(new { itemStatuses, customerNames });
    }
}
