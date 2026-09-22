using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procument.Module.OurInventory.DTOs;
using Procument.Module.OurInventory.Services;
using Procument.Shared.Audit;
using Procument.Shared.DTOs;

namespace Procument.Module.OurInventory.Controllers;

/// <summary>Read side of Our Stock. Cost and value are only returned to Admin / SuperAdmin.</summary>
[ApiController]
[Route("api/our-inventory")]
[Authorize(Roles = "Admin,SuperAdmin,Expert,Payment,AHM,Inventory")]
public sealed class OurStockController(IStockQueryService stock, IStockManagementService manage) : ControllerBase
{
    private bool CanSeeCost => User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
    private long UserId => long.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

    [HttpGet("stock")]
    public async Task<ActionResult<PagedResult<StockItemResponse>>> GetAll([FromQuery] StockItemQuery query)
        => Ok(await stock.GetAllAsync(query, CanSeeCost));

    [HttpGet("stock/filter-options")]
    public async Task<ActionResult<StockItemFilterOptions>> GetFilterOptions([FromQuery] StockItemQuery query)
        => Ok(await stock.GetFilterOptionsAsync(query));

    /// <summary>All matching lots grouped by company and warehouse (no paging). Values only for Admin / SuperAdmin.</summary>
    [HttpGet("stock/valuation")]
    public async Task<ActionResult<StockValuationResponse>> GetValuation([FromQuery] StockItemQuery query)
        => Ok(await stock.GetValuationAsync(query, CanSeeCost));

    [HttpGet("stock/{id:long}")]
    public async Task<ActionResult<StockItemDetailResponse>> GetById(long id)
        => await stock.GetByIdAsync(id, CanSeeCost) is { } detail ? Ok(detail) : NotFound();

    /// <summary>Every lot of one part (all warehouses and companies) — used by RFQ / quote dialogs.</summary>
    [HttpGet("stock/by-part/{partNumberId:long}")]
    public async Task<ActionResult<List<StockItemResponse>>> GetByPart(long partNumberId)
        => Ok((await stock.GetAllAsync(new StockItemQuery { PartNumberId = partNumberId, PageSize = -1 }, CanSeeCost)).Items);

    /// <summary>Bin, minimum quantity, tag date and notes. Quantities only change through movements.</summary>
    [HttpPatch("stock/{id:long}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [Auditable("OurStockItem", "Update", CaptureBody = true)]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateStockItemRequest request)
    {
        try { return await manage.UpdateAsync(id, request) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("stock/{id:long}/adjust")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [Auditable("OurStockItem", "Adjust", CaptureBody = true)]
    public Task<IActionResult> Adjust(long id, [FromBody] StockAdjustRequest request)
        => Run(() => manage.AdjustAsync(id, request, UserId));

    [HttpPost("stock/{id:long}/transfer")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [Auditable("OurStockItem", "Transfer", CaptureBody = true)]
    public Task<IActionResult> Transfer(long id, [FromBody] StockTransferRequest request)
        => Run(() => manage.TransferAsync(id, request, UserId));

    /// <summary>Opening balance / bulk import. All rows are validated first; nothing is saved if any row fails.</summary>
    [HttpPost("stock/opening")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [Auditable("OurStockItem", "OpeningImport", CaptureBody = false)]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<OpeningStockResult>> ImportOpening([FromBody] OpeningStockRequest request)
    {
        try
        {
            var result = await manage.ImportOpeningAsync(request, UserId);
            return result.Errors.Count > 0 ? BadRequest(result) : Ok(result);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>Ship reserved stock to the customer (Issue movement). Defaults to the whole reservation.</summary>
    [HttpPost("reservations/{id:long}/issue")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [Auditable("OurStockReservation", "Issue", CaptureBody = true)]
    public Task<IActionResult> IssueReservation(long id, [FromBody] IssueReservationRequest? request)
        => Run(() => manage.IssueReservationAsync(id, request ?? new IssueReservationRequest(), UserId));

    /// <summary>Give reserved stock back to available without shipping it.</summary>
    [HttpPost("reservations/{id:long}/release")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [Auditable("OurStockReservation", "Release")]
    public Task<IActionResult> ReleaseReservation(long id)
        => Run(() => manage.ReleaseReservationAsync(id, UserId));

    [HttpGet("movements")]
    public async Task<ActionResult<PagedResult<StockMovementResponse>>> GetMovements([FromQuery] StockMovementQuery query)
        => Ok(await stock.GetMovementsAsync(query, CanSeeCost));

    /// <summary>Approved Stock PO lines still waiting to be received.</summary>
    [HttpGet("incoming")]
    public async Task<ActionResult<List<IncomingStockLineResponse>>> GetIncoming([FromQuery] long? warehouseId, [FromQuery] long? partNumberId)
        => Ok(await stock.GetIncomingAsync(warehouseId, partNumberId));

    private async Task<IActionResult> Run(Func<Task> action)
    {
        if (UserId <= 0) return Unauthorized();
        try { await action(); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
