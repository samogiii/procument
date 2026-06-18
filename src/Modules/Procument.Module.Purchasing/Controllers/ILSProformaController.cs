using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Services;

namespace Procument.Module.Purchasing.Controllers;

[ApiController]
[Route("api/ils-proforma")]
[Authorize(Roles = "Admin,SuperAdmin,Expert")]
public class ILSProformaController : ControllerBase
{
    private readonly IILSProformaService _service;
    public ILSProformaController(IILSProformaService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult> GetAll()
    {
        var items = await _service.GetAllAsync();
        return Ok(items);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult> GetById(long id)
    {
        var item = await _service.GetByIdAsync(id);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] CreateILSProformaRequest request)
    {
        try
        {
            var result = await _service.CreateAsync(request);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPatch("{id:long}/status")]
    public async Task<ActionResult> UpdateStatus(long id, [FromBody] UpdateILSProformaStatusRequest request)
    {
        try
        {
            var result = await _service.UpdateStatusAsync(id, request.Status);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:long}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult> Delete(long id)
    {
        var deleted = await _service.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}
