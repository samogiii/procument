using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Services;
using Procument.Shared.Audit;

namespace Procument.Module.Purchasing.Controllers;

[ApiController]
[Route("api/purchase-orders")]
[Authorize(Roles = "Admin,Expert")]
public class PurchaseOrdersController : ControllerBase
{
    private readonly IPurchaseOrderService _poService;

    public PurchaseOrdersController(IPurchaseOrderService poService)
    {
        _poService = poService;
    }

    /// <summary>Get all purchase orders.</summary>
    [HttpGet]
    public async Task<ActionResult<List<POResponse>>> GetAll()
    {
        var result = await _poService.GetAllAsync();
        return Ok(result);
    }

    /// <summary>Get all unassigned POItems (not yet assigned to a PO).</summary>
    [HttpGet("unassigned-items")]
    public async Task<ActionResult<List<UnassignedPOItemResponse>>> GetUnassignedItems()
    {
        var result = await _poService.GetUnassignedItemsAsync();
        return Ok(result);
    }

    /// <summary>Get a purchase order by ID.</summary>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<POResponse>> GetById(long id)
    {
        var result = await _poService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Create a new purchase order.</summary>
    [HttpPost]
    [Auditable("PurchaseOrder", "Create", CaptureBody = true)]
    public async Task<ActionResult<POResponse>> Create([FromBody] CreatePORequest request)
    {
        var result = await _poService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Update purchase order status.</summary>
    [HttpPatch("{id:long}/status")]
    [Auditable("PurchaseOrder", "UpdateStatus", CaptureBody = true)]
    public async Task<IActionResult> UpdateStatus(long id, [FromBody] UpdatePOStatusRequest request)
    {
        var success = await _poService.UpdateStatusAsync(id, request.Status);
        return success ? Ok() : NotFound();
    }

    /// <summary>Update a single PO item (supplier, qty, unitPrice).</summary>
    [HttpPut("items/{id:long}")]
    [Auditable("POItem", "Update", CaptureBody = true)]
    public async Task<IActionResult> UpdateItem(long id, [FromBody] UpdatePOItemRequest request)
    {
        request.Id = id;
        var success = await _poService.UpdateItemAsync(request);
        return success ? Ok() : NotFound();
    }

    /// <summary>Delete a purchase order.</summary>
    [HttpDelete("{id:long}")]
    [Auditable("PurchaseOrder", "Delete")]
    public async Task<IActionResult> Delete(long id)
    {
        var deleted = await _poService.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}
