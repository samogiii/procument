using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Services;

namespace Procument.Module.Purchasing.Controllers;

/// <summary>
/// Warehouse-to-warehouse stock movement. Creating a transfer emits a destination track-number leg,
/// so the receiving warehouse verifies the goods through the normal Shipping flow.
/// </summary>
[ApiController]
[Route("api/warehouse-transfers")]
[Authorize(Roles = "SuperAdmin,Expert")]
public class WarehouseTransfersController : ControllerBase
{
    private readonly IWarehouseTransferService _service;
    private readonly IWarehouseService _warehouseService;
    private readonly IShippingService _shippingService;

    public WarehouseTransfersController(
        IWarehouseTransferService service,
        IWarehouseService warehouseService,
        IShippingService shippingService)
    {
        _service = service;
        _warehouseService = warehouseService;
        _shippingService = shippingService;
    }

    private long GetUserId()
    {
        var str = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!long.TryParse(str, out var id))
            throw new UnauthorizedAccessException("Invalid user token.");
        return id;
    }

    /// <summary>SuperAdmin and the SYD shipping account move stock between any warehouses.</summary>
    private bool IsUnrestricted() => User.IsInRole("SuperAdmin") || User.Identity?.Name == "SYD";

    /// <summary>Null when the caller may act on any warehouse; otherwise their assigned warehouse ids.</summary>
    private async Task<IReadOnlyCollection<long>?> GetWarehouseScopeAsync()
        => IsUnrestricted() ? null : await _warehouseService.GetWarehouseIdsForUserAsync(GetUserId());

    // ── Transferable stock ────────────────────────────────────────────────

    /// <summary>Verified stock at a warehouse that is still free to move (not on a shipment note).</summary>
    [HttpGet("available")]
    public async Task<ActionResult<List<TransferableStockResponse>>> GetAvailable([FromQuery] long? warehouseId = null)
        => Ok(await _service.GetTransferableStockAsync(warehouseId, await GetWarehouseScopeAsync()));

    // ── Transfers ─────────────────────────────────────────────────────────

    /// <summary>List transfers, optionally filtered by warehouse (either side) or status.</summary>
    [HttpGet]
    public async Task<ActionResult<List<WarehouseTransferResponse>>> GetAll(
        [FromQuery] long? warehouseId = null,
        [FromQuery] string? status = null)
        => Ok(await _service.GetAllAsync(warehouseId, status));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<WarehouseTransferResponse>> GetById(long id)
    {
        var result = await _service.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Move selected items from one warehouse to another under a new track number.</summary>
    [HttpPost]
    public async Task<ActionResult<WarehouseTransferResponse>> Create([FromBody] CreateWarehouseTransferRequest request)
    {
        try
        {
            var result = await _service.CreateAsync(GetUserId(), request, await GetWarehouseScopeAsync());
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>Cancel an in-transit transfer and return the units to the source warehouse.</summary>
    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id)
    {
        try
        {
            var ok = await _service.CancelAsync(id, GetUserId());
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>
    /// Receive the whole transfer in one action: every destination leg is taken as arrived
    /// in full and all its parts are accepted. The goods were already verified at the source
    /// warehouse, so the PO's assigned users do not review them again on arrival.
    /// </summary>
    [HttpPost("{id:long}/receive-all")]
    public async Task<ActionResult<WarehouseTransferResponse>> ReceiveAll(
        long id,
        [FromBody] ReceiveTransferRequest? request = null)
    {
        var transfer = await _service.GetByIdAsync(id);
        if (transfer == null) return NotFound();

        if (transfer.Status is "Cancelled" or "Received")
            return BadRequest(new { message = $"Transfer is already {transfer.Status}." });

        var note = string.IsNullOrWhiteSpace(request?.Note)
            ? $"Received in full on transfer {transfer.TransferNumber}"
            : request!.Note!.Trim();

        foreach (var trackId in transfer.DestinationTrackNumberIds)
            await _shippingService.ReceiveAndAcceptAllAsync(trackId, GetUserId(), note);

        return Ok(await _service.GetByIdAsync(id));
    }

    // ── Trace ─────────────────────────────────────────────────────────────

    /// <summary>Full journey of one part: supplier → warehouse(s) → shipment note → office/customer.</summary>
    [HttpGet("/api/shipping/trace/{poItemId:long}")]
    [Authorize(Roles = "Admin,SuperAdmin,Expert,Inventory")]
    public async Task<ActionResult<ShippingTraceResponse>> GetTrace(long poItemId)
    {
        var result = await _service.GetTraceAsync(poItemId);
        return result == null ? NotFound() : Ok(result);
    }
}
