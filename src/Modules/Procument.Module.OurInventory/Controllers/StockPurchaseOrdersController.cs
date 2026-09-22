using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Identity.Entities;
using Procument.Module.OurInventory.DTOs;
using Procument.Module.OurInventory.Services;
using Procument.Shared.Audit;
using Procument.Shared.DTOs;

namespace Procument.Module.OurInventory.Controllers;

[ApiController]
[Route("api/our-inventory/purchase-orders")]
[Authorize(Roles = "Admin,SuperAdmin,Expert,Payment,AHM,Inventory")]
public sealed class StockPurchaseOrdersController(
    IStockPurchaseOrderService service,
    IStockReceiptService receipts,
    DbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<StockPurchaseOrderResponse>>> GetAll([FromQuery] StockPurchaseOrderQuery query)
        => Ok(await service.GetAllAsync(query));

    [HttpGet("filter-options")]
    public async Task<ActionResult<StockPurchaseOrderFilterOptions>> GetFilterOptions([FromQuery] StockPurchaseOrderQuery query)
        => Ok(await service.GetFilterOptionsAsync(query));

    [HttpGet("suggest")]
    public async Task<ActionResult<List<StockPurchaseSuggestion>>> Suggest([FromQuery] long? companyPresetId, [FromQuery] long? warehouseId)
        => Ok(await service.SuggestAsync(companyPresetId, warehouseId));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<StockPurchaseOrderResponse>> GetById(long id)
        => await service.GetByIdAsync(id) is { } row ? Ok(row) : NotFound();

    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin,Expert,Inventory")]
    [Auditable("PurchaseOrder", "CreateStock", CaptureBody = true)]
    public async Task<ActionResult<StockPurchaseOrderResponse>> Create([FromBody] SaveStockPurchaseOrderRequest request)
    {
        try
        {
            var result = await service.CreateAsync(request);
            var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (long.TryParse(idClaim, out var userId) && userId > 0 && !User.IsInRole("Admin") && !User.IsInRole("SuperAdmin"))
            {
                db.Set<EntityPermission>().Add(new EntityPermission
                {
                    UserId = userId,
                    EntityName = "PO",
                    EntityId = result.Id.ToString(),
                    Permission = "Edit",
                    CreatedAt = DateTime.UtcNow,
                });
                await db.SaveChangesAsync();
            }
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:long}")]
    [Authorize(Roles = "Admin,SuperAdmin,Expert,Inventory")]
    [Auditable("PurchaseOrder", "UpdateStock", CaptureBody = true)]
    public async Task<ActionResult<StockPurchaseOrderResponse>> Update(long id, [FromBody] SaveStockPurchaseOrderRequest request)
    {
        try { return await service.UpdateAsync(id, request) is { } row ? Ok(row) : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:long}/submit")]
    [Authorize(Roles = "Admin,SuperAdmin,Expert,Inventory")]
    [Auditable("PurchaseOrder", "SubmitStock")]
    public async Task<ActionResult<StockPurchaseOrderResponse>> Submit(long id)
    {
        try { return await service.SubmitAsync(id) is { } row ? Ok(row) : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>Ordered / received / remaining per line, with the receipt movements behind it.</summary>
    [HttpGet("{id:long}/receipts")]
    public async Task<ActionResult<StockReceiptSummaryResponse>> GetReceipts(long id)
        => await receipts.GetSummaryAsync(id) is { } summary ? Ok(summary) : NotFound();

    /// <summary>Receives stock that arrived without a track number.</summary>
    [HttpPost("{id:long}/receive")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [Auditable("PurchaseOrder", "ReceiveStock", CaptureBody = true)]
    public async Task<ActionResult<StockReceiptSummaryResponse>> Receive(long id, [FromBody] ManualStockReceiptRequest request)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try { return await receipts.ReceiveManualAsync(id, request, userId) is { } summary ? Ok(summary) : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>Attaches serial numbers to a receipt, e.g. one booked from a track number.</summary>
    [HttpPost("~/api/our-inventory/receipts/{movementId:long}/serials")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [Auditable("OurStockMovement", "AddSerials", CaptureBody = true)]
    public async Task<IActionResult> AddSerials(long movementId, [FromBody] AddStockSerialsRequest request)
    {
        try { return Ok(new { added = await receipts.AddSerialsAsync(movementId, request.Serials) }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    private bool TryGetUserId(out long userId)
        => long.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out userId) && userId > 0;
}
