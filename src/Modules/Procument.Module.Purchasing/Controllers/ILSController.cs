using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Services;

namespace Procument.Module.Purchasing.Controllers;

[ApiController]
[Route("api/ils")]
[Authorize(Roles = "Admin,SuperAdmin,Expert")]
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

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ILSItemResponse>> GetById(long id)
    {
        var item = await _ilsService.GetByIdAsync(id);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<ILSItemResponse>> Save([FromBody] SaveILSItemRequest request)
    {
        try
        {
            var result = await _ilsService.SaveAsync(request);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpDelete("{id:long}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult> Delete(long id)
    {
        var deleted = await _ilsService.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }

    [HttpGet("ar-shop-suggestions")]
    [Authorize(Roles = "Admin,SuperAdmin")]
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

    // ── Serials ──────────────────────────────────────────────────────────────

    [HttpGet("{ilsItemId:long}/serials")]
    public async Task<ActionResult<List<ILSSerialResponse>>> GetSerials(long ilsItemId)
    {
        var serials = await _ilsService.GetSerialsAsync(ilsItemId);
        return Ok(serials);
    }

    [HttpPost("serials")]
    public async Task<ActionResult<ILSSerialResponse>> SaveSerial([FromBody] SaveILSSerialRequest request)
    {
        try
        {
            var result = await _ilsService.SaveSerialAsync(request);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpDelete("serials/{id:long}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult> DeleteSerial(long id)
    {
        var deleted = await _ilsService.DeleteSerialAsync(id);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPost("serials/{id:long}/{kind}-image")]
    [RequestSizeLimit(50_000_000)]
    public async Task<ActionResult<ILSSerialResponse>> UploadSerialImage(long id, string kind, IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest(new { error = "No file provided." });
        try
        {
            var result = await _ilsService.UploadSerialImageAsync(id, kind, file);
            return result == null ? NotFound() : Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("serials/{id:long}/{kind}-image")]
    public async Task<IActionResult> GetSerialImage(long id, string kind)
    {
        try
        {
            var result = await _ilsService.GetSerialImageAsync(id, kind);
            if (result == null) return NotFound();
            var (stream, fileName, mimeType) = result.Value;
            return File(stream, mimeType, fileName);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("serials/{id:long}/{kind}-image")]
    public async Task<ActionResult<ILSSerialResponse>> DeleteSerialImage(long id, string kind)
    {
        try
        {
            var result = await _ilsService.DeleteSerialImageAsync(id, kind);
            return result == null ? NotFound() : Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
