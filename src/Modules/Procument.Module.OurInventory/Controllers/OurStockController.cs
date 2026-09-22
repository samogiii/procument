using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procument.Module.OurInventory.DTOs;
using Procument.Module.OurInventory.Services;
using Procument.Shared.DTOs;

namespace Procument.Module.OurInventory.Controllers;

/// <summary>Read side of Our Stock. Cost and value are only returned to Admin / SuperAdmin.</summary>
[ApiController]
[Route("api/our-inventory")]
[Authorize(Roles = "Admin,SuperAdmin,Expert,Payment,AHM,Inventory")]
public sealed class OurStockController(IStockQueryService stock) : ControllerBase
{
    private bool CanSeeCost => User.IsInRole("Admin") || User.IsInRole("SuperAdmin");

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

    [HttpGet("movements")]
    public async Task<ActionResult<PagedResult<StockMovementResponse>>> GetMovements([FromQuery] StockMovementQuery query)
        => Ok(await stock.GetMovementsAsync(query, CanSeeCost));

    /// <summary>Approved Stock PO lines still waiting to be received.</summary>
    [HttpGet("incoming")]
    public async Task<ActionResult<List<IncomingStockLineResponse>>> GetIncoming([FromQuery] long? warehouseId, [FromQuery] long? partNumberId)
        => Ok(await stock.GetIncomingAsync(warehouseId, partNumberId));
}
