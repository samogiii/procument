using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Services;

namespace Procument.Module.Purchasing.Controllers;

[ApiController]
[Route("api/caplist")]
[Authorize(Roles = "Admin,SuperAdmin,Expert")]
public class CapListController : ControllerBase
{
    private readonly ICapListService _capListService;

    public CapListController(ICapListService capListService)
    {
        _capListService = capListService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _capListService.GetAllAsync();
        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] SaveCapListItemRequest request)
    {
        try
        {
            var result = await _capListService.SaveAsync(request);
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
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Delete(long id)
    {
        var deleted = await _capListService.DeleteAsync(id);
        return deleted ? Ok() : NotFound();
    }

    [HttpGet("ar-shop-suggestions")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> GetARShopSuggestions()
    {
        var suggestions = await _capListService.GetARShopSuggestionsAsync();
        return Ok(suggestions);
    }

    [HttpPost("bulk-import")]
    public async Task<IActionResult> BulkImport([FromBody] BulkImportCapListRequest request)
    {
        var result = await _capListService.BulkImportAsync(request);
        return Ok(result);
    }
}
