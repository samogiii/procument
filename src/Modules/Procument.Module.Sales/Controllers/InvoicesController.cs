using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procument.Module.Sales.DTOs;
using Procument.Module.Sales.Services;
using Procument.Shared.Audit;
using System.Security.Claims;

namespace Procument.Module.Sales.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InvoicesController : ControllerBase
{
    private readonly IInvoiceService _invoiceService;

    public InvoicesController(IInvoiceService invoiceService)
    {
        _invoiceService = invoiceService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<InvoiceResponse>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var (userId, isAdmin) = GetUserContext();
        var result = await _invoiceService.GetAllAsync(page, pageSize, userId, isAdmin);
        return Ok(result);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<InvoiceResponse>> GetById(long id)
    {
        var (userId, isAdmin) = GetUserContext();
        var result = await _invoiceService.GetByIdAsync(id, userId, isAdmin);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Auditable("Invoice", "Create", CaptureBody = true)]
    public async Task<ActionResult<InvoiceResponse>> Create([FromBody] CreateInvoiceRequest request)
    {
        var (userId, _) = GetUserContext();
        var result = await _invoiceService.CreateAsync(request, userId);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPatch("{id:long}/status")]
    [Auditable("Invoice", "UpdateStatus", CaptureBody = true)]
    public async Task<IActionResult> UpdateStatus(long id, [FromBody] UpdateInvoiceStatusRequest request)
    {
        var (userId, isAdmin) = GetUserContext();
        var success = await _invoiceService.UpdateStatusAsync(id, request.Status, userId, isAdmin);
        return success ? Ok() : NotFound();
    }

    [HttpPost("permissions")]
    [Auditable("Invoice", "GrantPermissions", CaptureBody = true)]
    public async Task<IActionResult> GrantPermissions([FromBody] GrantPermissionRequest request)
    {
        var (userId, isAdmin) = GetUserContext();
        if (!isAdmin) return Forbid();

        var success = await _invoiceService.GrantPermissionsAsync(request.InvoiceIds, request.TargetUserId, request.Permission);
        return success ? Ok() : BadRequest("Failed to grant permissions.");
    }

    private (long userId, bool isAdmin) GetUserContext()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        long userId = 0;
        if (idClaim != null && long.TryParse(idClaim.Value, out var id))
        {
            userId = id;
        }
        bool isAdmin = User.IsInRole("Admin");
        return (userId, isAdmin);
    }
}
