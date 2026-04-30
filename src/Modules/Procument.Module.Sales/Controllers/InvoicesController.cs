using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Sales.DTOs;
using Procument.Module.Sales.Entities;
using Procument.Module.Sales.Services;
using Procument.Module.Identity.Entities;
using Procument.Shared.Audit;
using Procument.Shared.DTOs;
using Procument.Shared.Entities;
using Procument.Shared.Services;
using System.Security.Claims;

namespace Procument.Module.Sales.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class InvoicesController : ControllerBase
{
    private readonly IInvoiceService _invoiceService;
    private readonly DbContext _db;
    private readonly IFinalInvoiceLockGuard _lockGuard;

    public InvoicesController(IInvoiceService invoiceService, DbContext db, IFinalInvoiceLockGuard lockGuard)
    {
        _invoiceService = invoiceService;
        _db = db;
        _lockGuard = lockGuard;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<InvoiceResponse>>> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 200,
        [FromQuery] string? status = null, [FromQuery] string? customer = null)
    {
        var pq = new PageQuery { Page = page, PageSize = pageSize };
        var (userId, isAdmin) = GetUserContext();
        var result = await _invoiceService.GetAllAsync(pq, userId, isAdmin, status, customer);
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

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateInvoiceRequest request)
    {
        var success = await _invoiceService.UpdateAsync(id, request);
        return success ? Ok() : NotFound();
    }

    [HttpPatch("{id:long}/items")]
    public async Task<IActionResult> UpdateItems(long id, [FromBody] UpdateInvoiceItemsRequest request)
    {
        var success = await _invoiceService.UpdateItemsAsync(id, request);
        return success ? Ok() : NotFound();
    }

    [HttpPatch("{id:long}/status")]
    [Auditable("Invoice", "UpdateStatus", CaptureBody = true)]
    public async Task<IActionResult> UpdateStatus(long id, [FromBody] UpdateInvoiceStatusRequest request)
    {
        if (await _lockGuard.IsInvoiceLocked(id))
            return BadRequest(new { message = "This Proforma Invoice is locked because a Final Invoice has been created." });

        var (userId, isAdmin) = GetUserContext();
        // Get invoice info for notification
        var invoice = await _invoiceService.GetByIdAsync(id, userId, isAdmin);
        if (invoice == null) return NotFound();

        var success = await _invoiceService.UpdateStatusAsync(id, request.Status, userId, isAdmin, request.RejectionNote);
        if (!success) return BadRequest("Status change not allowed.");

        // Create notifications
        if (request.Status == "Rejected" || request.Status == "Paid")
        {
            // Notify the invoice owner (via quote)
            var ownerUserId = await _db.Set<Invoice>()
                .Where(i => i.Id == id)
                .Select(i => i.Quote.UserId)
                .FirstOrDefaultAsync();
            if (ownerUserId > 0)
            {
                var msg = request.Status == "Rejected"
                    ? $"Proforma Invoice {invoice.InvoiceNumber} has been rejected."
                    : $"Proforma Invoice {invoice.InvoiceNumber} has been marked as Paid.";
                _db.Set<Notification>().Add(new Notification
                {
                    UserId = ownerUserId,
                    Type = request.Status == "Rejected" ? "Rejection" : "StatusChange",
                    EntityName = "Invoice",
                    EntityId = id,
                    EntityNumber = invoice.InvoiceNumber,
                    Message = msg,
                    RejectionNote = request.RejectionNote
                });
                await _db.SaveChangesAsync();
            }
        }
        else if (request.Status == "Pending")
        {
            // Notify admins
            var adminIds = await _db.Set<User>().Where(u => (u.Role == "Admin" || u.Role == "SuperAdmin") && u.IsActive).Select(u => u.Id).ToListAsync();
            foreach (var aid in adminIds)
            {
                _db.Set<Notification>().Add(new Notification
                {
                    UserId = aid,
                    Type = "PendingApproval",
                    EntityName = "Invoice",
                    EntityId = id,
                    EntityNumber = invoice.InvoiceNumber,
                    Message = $"Proforma Invoice {invoice.InvoiceNumber} is pending approval."
                });
            }
            await _db.SaveChangesAsync();
        }

        return Ok();
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
        bool isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
        return (userId, isAdmin);
    }
}
