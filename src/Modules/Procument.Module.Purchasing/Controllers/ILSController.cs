using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Services;

namespace Procument.Module.Purchasing.Controllers;

[ApiController]
[Route("api/ils")]
[Authorize(Roles = "Admin,Expert")]
public class ILSController : ControllerBase
{
    private readonly IILSService _ilsService;

    public ILSController(IILSService ilsService) => _ilsService = ilsService;

    [HttpGet]
    public async Task<ActionResult<List<ILSItemResponse>>> GetAll()
    {
        var items = await _ilsService.GetAllAsync();
        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<ILSItemResponse>> Save([FromBody] SaveILSItemRequest request)
    {
        try
        {
            var result = await _ilsService.SaveAsync(request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:long}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Delete(long id)
    {
        var deleted = await _ilsService.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }

    [HttpGet("ar-shop-suggestions")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<ARShopSuggestionResponse>>> GetARShopSuggestions()
    {
        var suggestions = await _ilsService.GetARShopSuggestionsAsync();
        return Ok(suggestions);
    }

    [HttpPost("bulk-import")]
    public async Task<IActionResult> BulkImport([FromBody] BulkImportILSRequest request)
    {
        var result = await _ilsService.BulkImportAsync(request);
        return Ok(result);
    }
}
