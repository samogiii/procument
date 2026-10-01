using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Identity.Entities;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Services;

namespace Procument.Module.Purchasing.Controllers;

[ApiController]
[Route("api/inventory")]
[Authorize(Roles = "Admin,SuperAdmin,Expert")]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;
    private readonly DbContext _db;

    public InventoryController(IInventoryService inventoryService, DbContext db)
    {
        _inventoryService = inventoryService;
        _db = db;
    }

    /// <summary>
    /// Adding items and Excel import are limited to users granted "Amir Access" (isAmir) in
    /// Menu Access. SuperAdmin always passes, as with every other isAmir check.
    /// </summary>
    private async Task<bool> CanAddItemsAsync()
    {
        if (User.IsInRole("SuperAdmin")) return true;
        var userName = User.Identity?.Name;
        return !string.IsNullOrEmpty(userName) && await _db.Set<MenuPermission>()
            .AnyAsync(p => p.Feature == "isAmir" && p.UserName == userName);
    }

    private ObjectResult AddItemsForbidden() =>
        StatusCode(StatusCodes.Status403Forbidden, new { error = "Only users with Amir Access can add or import inventory items." });

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _inventoryService.GetAllAsync();
        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] SaveInventoryItemRequest request)
    {
        // A new item (no Id) needs Amir Access; editing an existing one does not.
        if ((request.Id ?? 0) == 0 && !await CanAddItemsAsync()) return AddItemsForbidden();
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

    [HttpDelete("suppliers/{supplierId:long}")]
    public async Task<IActionResult> DeleteSupplier(long supplierId)
    {
        var deletedItems = await _inventoryService.DeleteSupplierAsync(supplierId);
        if (deletedItems == 0) return NotFound(new { error = "Supplier has no inventory items." });

        return Ok(new DeleteSupplierInventoryResponse
        {
            SupplierId = supplierId,
            DeletedItems = deletedItems
        });
    }

    [HttpPost("bulk-import")]
    public async Task<IActionResult> BulkImport([FromBody] BulkImportInventoryRequest request)
    {
        if (!await CanAddItemsAsync()) return AddItemsForbidden();
        var result = await _inventoryService.BulkImportAsync(request);
        return Ok(result);
    }
}
