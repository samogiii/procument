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
        [FromQuery] string? status = null, [FromQuery] string? customer = null,
        [FromQuery] string? sortBy = null, [FromQuery] bool sortDesc = false,
        [FromQuery] List<string>? customerCodes = null,
        [FromQuery] List<string>? statuses = null,
        [FromQuery] List<string>? invoiceNumbers = null)
    {
        var pq = new PageQuery { Page = page, PageSize = pageSize };
        var (userId, isAdmin, isSuperAdmin, userBases) = GetUserContext();
        var result = await _invoiceService.GetAllAsync(pq, userId, isAdmin, status, customer, sortBy, sortDesc, customerCodes, statuses, invoiceNumbers, isSuperAdmin, userBases);
        return Ok(result);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<InvoiceResponse>> GetById(long id)
    {
        var (userId, isAdmin, isSuperAdmin, userBases) = GetUserContext();
        var result = await _invoiceService.GetByIdAsync(id, userId, isAdmin);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("by-quote/{quoteId:long}")]
    public async Task<ActionResult<List<InvoiceResponse>>> GetByQuote(long quoteId)
    {
        var (userId, isAdmin, _, _) = GetUserContext();
        var ids = await _db.Set<Invoice>()
            .Where(i => i.QuoteId == quoteId)
            .Select(i => i.Id)
            .ToListAsync();
        var results = new List<InvoiceResponse>();
        foreach (var id in ids)
        {
            var inv = await _invoiceService.GetByIdAsync(id, userId, isAdmin);
            if (inv != null) results.Add(inv);
        }
        return Ok(results);
    }

    [HttpPost]
    [Auditable("Invoice", "Create", CaptureBody = true)]
    public async Task<ActionResult<InvoiceResponse>> Create([FromBody] CreateInvoiceRequest request)
    {
        var (userId, _, _, _) = GetUserContext();
        var result = await _invoiceService.CreateAsync(request, userId);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPatch("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateInvoiceRequest request)
    {
        var success = await _invoiceService.UpdateAsync(id, request);
        return success ? Ok() : NotFound();
    }

    [HttpPatch("{id:long}/totals")]
    [Auditable("Invoice", "UpdateTotals")]
    public async Task<IActionResult> UpdateTotals(long id, [FromBody] UpdateInvoiceTotalsRequest request)
    {
        var invoice = await _db.Set<Invoice>().FindAsync(id);
        if (invoice == null) return NotFound();

        invoice.Tax = request.Tax;
        invoice.Shipping = request.Shipping;
        invoice.ProcessingFee = request.ProcessingFee;

        await _db.SaveChangesAsync();
        return Ok();
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

        var (userId, isAdmin, isSuperAdmin, userBases) = GetUserContext();
        // Get invoice info for notification
        var invoice = await _invoiceService.GetByIdAsync(id, userId, isAdmin);
        if (invoice == null) return NotFound();

        bool success;
        try
        {
            success = await _invoiceService.UpdateStatusAsync(id, request.Status, userId, isAdmin, request.AutoFinalize);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        if (!success) return BadRequest(new { message = "Status change not allowed." });

        // Create notifications
        if (request.Status == "Finish")
        {
            // Notify the invoice owner that the invoice is finished/paid
            var ownerUserId = await _db.Set<Invoice>()
                .Where(i => i.Id == id)
                .Select(i => i.Quote.UserId)
                .FirstOrDefaultAsync();
            if (ownerUserId > 0)
            {
                _db.Set<Notification>().Add(new Notification
                {
                    UserId = ownerUserId,
                    Type = "StatusChange",
                    EntityName = "Invoice",
                    EntityId = id,
                    EntityNumber = invoice.InvoiceNumber,
                    Message = $"Proforma Invoice {invoice.InvoiceNumber} has been marked as Finished."
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

    [HttpPost("{id:long}/cancel")]
    [Auditable("Invoice", "Cancel")]
    public async Task<IActionResult> Cancel(long id)
    {
        if (await _lockGuard.IsInvoiceLocked(id))
            return BadRequest(new { message = "This Proforma Invoice is locked because a Final Invoice has been created." });

        var success = await _invoiceService.CancelAsync(id);
        return success ? Ok() : NotFound();
    }

    [HttpPatch("{id:long}/default-wallet")]
    public async Task<IActionResult> SetDefaultWallet(long id, [FromBody] SetDefaultWalletRequest request)
    {
        var invoice = await _db.Set<Invoice>().FindAsync(id);
        if (invoice == null) return NotFound();
        invoice.DefaultDepositWalletId = request.WalletId;
        await _db.SaveChangesAsync();
        return Ok(new { walletId = invoice.DefaultDepositWalletId });
    }

    [HttpGet("{id:long}/prepayment-check")]
    public async Task<IActionResult> GetPrepaymentCheck(long id)
    {
        var result = await _invoiceService.GetPrepaymentCheckAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost("permissions")]
    [Auditable("Invoice", "GrantPermissions", CaptureBody = true)]
    public async Task<IActionResult> GrantPermissions([FromBody] GrantPermissionRequest request)
    {
        var (userId, isAdmin, isSuperAdmin, userBases) = GetUserContext();
        if (!isAdmin) return Forbid();

        var success = await _invoiceService.GrantPermissionsAsync(request.InvoiceIds, request.TargetUserId, request.Permission);
        return success ? Ok() : BadRequest("Failed to grant permissions.");
    }

    [HttpGet("filter-options")]
    public async Task<ActionResult> GetInvoiceFilterOptions()
    {
        var query = _db.Set<Invoice>().AsNoTracking();
        var statuses = await query.Select(i => i.Status ?? "Draft").Distinct().OrderBy(s => s).ToListAsync();
        var customers = await query
            .Where(i => i.Customer != null)
            .Select(i => new { code = i.Customer!.CustomerCode, name = i.Customer!.Name })
            .Distinct()
            .ToListAsync();
        var invoiceNumbers = await query
            .Select(i => i.InvoiceNumber)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync();
        return Ok(new
        {
            statuses,
            customers = customers.GroupBy(c => c.code).Select(g => g.First()).OrderBy(c => c.code).ToList(),
            invoiceNumbers
        });
    }

    private (long userId, bool isAdmin, bool isSuperAdmin, int[] userBases) GetUserContext()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        long userId = 0;
        if (idClaim != null && long.TryParse(idClaim.Value, out var id))
            userId = id;
        bool isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
        bool isSuperAdmin = User.IsInRole("SuperAdmin");
        var basesClaim = User.FindFirst("bases")?.Value ?? "";
        int[] userBases = basesClaim.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var b) ? b : -1)
            .Where(b => b > 0).ToArray();
        return (userId, isAdmin, isSuperAdmin, userBases);
    }
}
