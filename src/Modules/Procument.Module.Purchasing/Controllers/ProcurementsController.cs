using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procument.Module.Purchasing.DTOs;
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

    public ProcurementsController(IProcurementService service)
    {
        _service = service;
    }

    private (long userId, bool isAdmin) GetCurrentUser()
    {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        long.TryParse(userIdStr, out var userId);
        var isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
        return (userId, isAdmin);
    }

    /// <summary>Paged list — admin/superadmin see all, others see only Procurements assigned to them.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProcurementResponse>>> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 200, [FromQuery] string? search = null)
    {
        var pq = new PageQuery { Page = page, PageSize = pageSize, Search = search };
        var (userId, isAdmin) = GetCurrentUser();
        var result = await _service.GetAllAsync(pq, userId, isAdmin);
        return Ok(result);
    }

    /// <summary>Full procurement detail with items + supplier quotes + assigned users.</summary>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<ProcurementResponse>> GetById(long id)
    {
        var (userId, isAdmin) = GetCurrentUser();
        var result = await _service.GetByIdAsync(id, userId, isAdmin);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Edit one procurement item (editable fields only — source/RFQ/Quote/Supplier snapshots are immutable).</summary>
    [HttpPatch("{id:long}/items/{itemId:long}")]
    [Auditable("Procurement", "UpdateItem", CaptureBody = true)]
    public async Task<IActionResult> UpdateItem(long id, long itemId, [FromBody] UpdateProcurementItemRequest request)
    {
        var (userId, isAdmin) = GetCurrentUser();
        if (!await _service.UserCanAccessItemAsync(id, itemId, userId, isAdmin)) return Forbid();

        var ok = await _service.UpdateItemAsync(id, itemId, request);
        return ok ? Ok() : BadRequest(new { message = "Unable to update item (finalized/cancelled or not found)." });
    }

    /// <summary>Add a new supplier quote or update one already attached to this item.</summary>
    [HttpPost("{id:long}/items/{itemId:long}/supplier-quotes")]
    [Auditable("Procurement", "UpsertSupplierQuote", CaptureBody = true)]
    public async Task<ActionResult<ProcurementSupplierQuoteResponse>> UpsertSupplierQuote(long id, long itemId, [FromBody] UpsertSupplierQuoteRequest request)
    {
        var (userId, isAdmin) = GetCurrentUser();
        if (!await _service.UserCanAccessItemAsync(id, itemId, userId, isAdmin)) return Forbid();

        var result = await _service.UpsertSupplierQuoteAsync(id, itemId, request, userId);
        return result == null ? BadRequest(new { message = "Unable to upsert supplier quote." }) : Ok(result);
    }

    /// <summary>Mark a supplier quote as selected — updates CurrentSupplierId / UnitPrice on the parent item.</summary>
    [HttpPost("{id:long}/items/{itemId:long}/supplier-quotes/{sqId:long}/select")]
    [Auditable("Procurement", "SelectSupplierQuote")]
    public async Task<IActionResult> SelectSupplierQuote(long id, long itemId, long sqId)
    {
        var (userId, isAdmin) = GetCurrentUser();
        if (!await _service.UserCanAccessItemAsync(id, itemId, userId, isAdmin)) return Forbid();

        var ok = await _service.SelectSupplierQuoteAsync(id, itemId, sqId);
        return ok ? Ok() : BadRequest(new { message = "Unable to select supplier quote." });
    }

    /// <summary>Delete a supplier quote from a procurement item.</summary>
    [HttpDelete("{id:long}/items/{itemId:long}/supplier-quotes/{sqId:long}")]
    [Auditable("Procurement", "DeleteSupplierQuote")]
    public async Task<IActionResult> DeleteSupplierQuote(long id, long itemId, long sqId)
    {
        var (userId, isAdmin) = GetCurrentUser();
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
        var (userId, _) = GetCurrentUser();
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
        var (userId, _) = GetCurrentUser();
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
        var (userId, _) = GetCurrentUser();
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
        var (userId, _) = GetCurrentUser();
        var ok = await _service.CancelAsync(id, userId);
        return ok ? Ok() : BadRequest(new { message = "Unable to cancel procurement." });
    }
}
