using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procument.Module.Purchasing.Services;

namespace Procument.Module.Purchasing.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaymentRequestsController : ControllerBase
{
    private readonly IPaymentRequestService _service;

    public PaymentRequestsController(IPaymentRequestService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<PaymentRequestResponse>>> GetAll()
    {
        return await _service.GetAllAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PaymentRequestResponse>> GetById(long id)
    {
        try
        {
            return await _service.GetByIdAsync(id);
        }
        catch (Exception ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("po/{poId}")]
    public async Task<ActionResult<PaymentRequestResponse>> GetByPoId(long poId)
    {
        var result = await _service.GetByPoIdAsync(poId);
        if (result == null) return NotFound();
        return result;
    }

    [HttpPost("po/{poId}")]
    public async Task<ActionResult<PaymentRequestResponse>> Create(long poId)
    {
        return await _service.CreateAsync(poId);
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(long id, [FromBody] string status)
    {
        var result = await _service.UpdateStatusAsync(id, status);
        if (!result) return NotFound();
        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _service.DeleteAsync(id);
        if (!result) return NotFound();
        return Ok();
    }
}
