using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Services;

namespace Procument.Module.Purchasing.Controllers;

// ════════════════════════════════════════════════════════════
//  ILS CUSTOMERS
// ════════════════════════════════════════════════════════════

[ApiController]
[Route("api/ils-customers")]
[Authorize(Roles = "Admin,Expert")]
public class ILSCustomersController : ControllerBase
{
    private readonly IILSQuoteService _service;
    public ILSCustomersController(IILSQuoteService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult> GetAll()
    {
        var items = await _service.GetAllCustomersAsync();
        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] ILSCustomerDto dto)
    {
        var result = await _service.SaveCustomerAsync(null, dto);
        return Ok(result);
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult> Update(long id, [FromBody] ILSCustomerDto dto)
    {
        try
        {
            var result = await _service.SaveCustomerAsync(id, dto);
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
        var deleted = await _service.DeleteCustomerAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}

// ════════════════════════════════════════════════════════════
//  ILS QUOTES
// ════════════════════════════════════════════════════════════

[ApiController]
[Route("api/ils-quotes")]
[Authorize(Roles = "Admin,Expert")]
public class ILSQuotesController : ControllerBase
{
    private readonly IILSQuoteService _service;
    public ILSQuotesController(IILSQuoteService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult> GetAll()
    {
        var items = await _service.GetAllQuotesAsync();
        return Ok(items);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult> GetById(long id)
    {
        var item = await _service.GetQuoteByIdAsync(id);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] CreateILSQuoteRequest request)
    {
        try
        {
            var result = await _service.CreateQuoteAsync(request);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult> Update(long id, [FromBody] CreateILSQuoteRequest request)
    {
        try
        {
            var result = await _service.UpdateQuoteAsync(id, request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPatch("{id:long}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> UpdateStatus(long id, [FromBody] UpdateILSQuoteStatusRequest request)
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
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Delete(long id)
    {
        var deleted = await _service.DeleteQuoteAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}
