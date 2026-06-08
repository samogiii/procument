using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procument.Module.Purchasing.Services;

namespace Procument.Module.Purchasing.Controllers;

/// <summary>
/// Manages the many-to-many link between Company Presets and Warehouses.
/// Lives in the Purchasing module so it can reference IWarehouseService without
/// creating a circular dependency with the Catalog module.
/// </summary>
[ApiController]
[Route("api/companypresets/{presetId:long}/warehouses")]
[Authorize(Roles = "Admin,SuperAdmin,Expert,Inventory")]
public class CompanyPresetWarehousesController : ControllerBase
{
    private readonly IWarehouseService _warehouseService;

    public CompanyPresetWarehousesController(IWarehouseService warehouseService)
    {
        _warehouseService = warehouseService;
    }

    /// <summary>Get warehouses linked to this Company Preset (used for Ship To selection).</summary>
    [HttpGet]
    public async Task<ActionResult> GetWarehouses(long presetId)
        => Ok(await _warehouseService.GetByCompanyPresetAsync(presetId));

    /// <summary>Link a warehouse to a Company Preset.</summary>
    [HttpPost("{warehouseId:long}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> LinkWarehouse(long presetId, long warehouseId)
    {
        await _warehouseService.LinkCompanyPresetAsync(presetId, warehouseId);
        return NoContent();
    }

    /// <summary>Unlink a warehouse from a Company Preset.</summary>
    [HttpDelete("{warehouseId:long}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> UnlinkWarehouse(long presetId, long warehouseId)
    {
        var ok = await _warehouseService.UnlinkCompanyPresetAsync(presetId, warehouseId);
        return ok ? NoContent() : NotFound();
    }
}
