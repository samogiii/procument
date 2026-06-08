using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Services;

namespace Procument.Module.Purchasing.Controllers;

[ApiController]
[Route("api/inventory")]
[Authorize(Roles = "Admin,SuperAdmin,Expert")]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public InventoryController(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _inventoryService.GetAllAsync();
        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] SaveInventoryItemRequest request)
    {
        try
        {
            var result = await _inventoryService.SaveAsync(request);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,SuperAdmin,Expert")]
    public async Task<IActionResult> Delete(long id)
    {
        var deleted = await _inventoryService.DeleteAsync(id);
        return deleted ? Ok() : NotFound();
    }

    [HttpPost("bulk-import")]
    public async Task<IActionResult> BulkImport([FromBody] BulkImportInventoryRequest request)
    {
        var result = await _inventoryService.BulkImportAsync(request);
        return Ok(result);
    }
}
