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
[Authorize(Roles = "Admin,SuperAdmin,Expert")]
public class QuotesController : ControllerBase
{
    private readonly IQuoteService _quoteService;
    private readonly DbContext _db;
    private readonly IFinalInvoiceLockGuard _lockGuard;

    public QuotesController(IQuoteService quoteService, DbContext db, IFinalInvoiceLockGuard lockGuard)
    {
        _quoteService = quoteService;
        _db = db;
        _lockGuard = lockGuard;
    }

    /// <summary>Get all quotes (paginated).</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<QuoteResponse>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] List<string>? status = null,
        [FromQuery] string? search = null,
        [FromQuery] string? pnSearch = null,
        [FromQuery] List<string>? assignedUserNames = null,
        [FromQuery] List<string>? customerNames = null,
        [FromQuery] List<string>? rfqNames = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDesc = false,
        [FromQuery] List<string>? quoteNumbers = null)
    {
        var (userId, isAdmin, isSuperAdmin, userBases) = GetUserContext();
        var result = await _quoteService.GetAllAsync(page, pageSize, userId, isSuperAdmin, userBases, status, search, pnSearch, assignedUserNames, customerNames, rfqNames, sortBy, sortDesc, quoteNumbers);
        return Ok(result);
    }

    /// <summary>Get all quotes for an RFQ.</summary>
    [HttpGet("by-rfq/{rfqId:long}")]
    public async Task<ActionResult<List<QuoteResponse>>> GetByRFQ(long rfqId)
    {
        var (userId, isAdmin, isSuperAdmin, userBases) = GetUserContext();
        var result = await _quoteService.GetByRFQIdAsync(rfqId, userId, isAdmin, userBases);
        return Ok(result);
    }

    /// <summary>Get a quote by ID.</summary>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<QuoteResponse>> GetById(long id)
    {
        var (userId, isAdmin, isSuperAdmin, userBases) = GetUserContext();
        var result = await _quoteService.GetByIdAsync(id, userId, isAdmin, userBases);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Create a new quote from selected procurement records.</summary>
    [HttpPost]
    [Auditable("Quote", "Create", CaptureBody = true)]
    public async Task<ActionResult<QuoteResponse>> Create([FromBody] CreateQuoteRequest request)
    {
        var (userId, isAdmin, isSuperAdmin, userBases) = GetUserContext();
        if (userId == 0) return Unauthorized("User ID not found in token.");

        try
        {
            var result = await _quoteService.CreateAsync(request, userId);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Update quote status.</summary>
    [HttpPatch("{id:long}/status")]
    [Auditable("Quote", "UpdateStatus", CaptureBody = true)]
    public async Task<IActionResult> UpdateStatus(long id, [FromBody] UpdateQuoteStatusRequest request)
    {
        if (await _lockGuard.IsQuoteLocked(id))
            return BadRequest(new { message = "This Quote is locked because a Final Invoice has been created." });

        var (userId, isAdmin, isSuperAdmin, userBases) = GetUserContext();
        // Get quote info before update for notification
        var quote = await _quoteService.GetByIdAsync(id, userId, isAdmin);
        if (quote == null) return NotFound();

        var success = await _quoteService.UpdateStatusAsync(id, request.Status, userId, isAdmin, request.RejectionNote);
        if (!success) return BadRequest("Status change not allowed.");

        // Create notifications
        if (request.Status == "Rejected" || request.Status == "Accepted")
        {
            // Notify the quote owner
            var ownerUser = await _db.Set<Quote>().Where(q => q.Id == id).Select(q => q.UserId).FirstOrDefaultAsync();
            if (ownerUser > 0)
            {
                var msg = request.Status == "Rejected"
                    ? $"Quote {quote.QuoteNumber} has been rejected."
                    : $"Quote {quote.QuoteNumber} has been accepted.";
                _db.Set<Notification>().Add(new Notification
                {
                    UserId = ownerUser,
                    Type = request.Status == "Rejected" ? "Rejection" : "StatusChange",
                    EntityName = "Quote",
                    EntityId = id,
                    EntityNumber = quote.QuoteNumber,
                    Message = msg,
                    RejectionNote = request.RejectionNote
                });
                await _db.SaveChangesAsync();
            }
        }
        else if (request.Status == "Sent")
        {
            // Notify all admins that a quote needs review
            var adminIds = await _db.Set<User>().Where(u => (u.Role == "Admin" || u.Role == "SuperAdmin") && u.IsActive).Select(u => u.Id).ToListAsync();
            foreach (var aid in adminIds)
            {
                _db.Set<Notification>().Add(new Notification
                {
                    UserId = aid,
                    Type = "PendingApproval",
                    EntityName = "Quote",
                    EntityId = id,
                    EntityNumber = quote.QuoteNumber,
                    Message = $"Quote {quote.QuoteNumber} is pending approval."
                });
            }
            await _db.SaveChangesAsync();
        }

        return Ok();
    }

    /// <summary>Update quote items (re-select procurement records).</summary>
    [HttpPut("{id:long}")]
    [Auditable("Quote", "Update", CaptureBody = true)]
    public async Task<ActionResult<QuoteResponse>> Update(long id, [FromBody] CreateQuoteRequest request)
    {
        var (userId, isAdmin, isSuperAdmin, userBases) = GetUserContext();
        var result = await _quoteService.UpdateAsync(id, request, userId, isAdmin);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Update quote Type.</summary>
    [HttpPatch("{id:long}/types")]
    [Auditable("Quote", "UpdateStatus", CaptureBody = true)]
    public async Task<IActionResult> UpdateQuoteType(long id, [FromBody] QuoteTypeDTO request)
    {
        var (userId, isAdmin, isSuperAdmin, userBases) = GetUserContext();
        var success = await _quoteService.UpdateQuoteTypeAsync(id, request.QuoteType,request.TypeAdditional, userId, isAdmin);
        return success ? Ok() : NotFound();
    }

    /// <summary>Search quotes by part number name.</summary>
    [HttpGet("search-by-pn")]
    public async Task<ActionResult> SearchByPartNumber([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return Ok(Array.Empty<object>());

        var (userId, isAdmin, isSuperAdmin, userBases) = GetUserContext();
        var query = _db.Set<QuoteItem>()
            .Include(qi => qi.PartNumber)
            .Include(qi => qi.Quote)
                .ThenInclude(quote => quote.Customer)
            .Where(qi => qi.PartNumber != null && (
                qi.PartNumber.Name.Contains(q) ||
                qi.Alt != null && qi.Alt.Contains(q) ||
                qi.PartNumber.Alternatives.Any(a => a.Name.Contains(q))
            ));

        if (!isSuperAdmin)
        {
            query = query.Where(qi =>
                qi.Quote.Customer == null ||
                qi.Quote.Customer.Base == null ||
                userBases.Contains(qi.Quote.Customer.Base.Value) ||
                qi.Quote.UserId == userId);
        }

        var results = await query
            .Select(qi => new
            {
                QuoteId = qi.Quote.Id,
                QuoteNumber = qi.Quote.QuoteNumber,
                PartNumberName = qi.PartNumber!.Name,
                MatchedAlt = qi.PartNumber.Alternatives.Where(a => a.Name.Contains(q)).Select(a => a.Name).FirstOrDefault() ?? (qi.Alt != null && qi.Alt.Contains(q) ? qi.Alt : null),
                CustomerName = qi.Quote.Customer.Name,
                Status = qi.Quote.Status,
                TotalAmount = qi.Quote.TotalAmount
            })
            .Distinct()
            .Take(20)
            .ToListAsync();

        return Ok(results);
    }

    /// <summary>Update quote items sort order.</summary>
    [HttpPatch("{id:long}/items-order")]
    [Auditable("Quote", "UpdateItemsOrder", CaptureBody = true)]
    public async Task<IActionResult> UpdateItemsOrder(long id, [FromBody] UpdateItemsOrderRequest request)
    {
        var (userId, isAdmin, isSuperAdmin, userBases) = GetUserContext();
        var ok = await _quoteService.UpdateItemsOrderAsync(id, request.Items, userId, isAdmin);
        return ok ? Ok() : NotFound();
    }

    /// <summary>Update RFQ ExType via Quote ID.</summary>
    [HttpPatch("{id:long}/rfq-ex-type")]
    [Auditable("Quote", "UpdateRFQExType", CaptureBody = true)]
    public async Task<IActionResult> UpdateRFQExType(long id, [FromBody] int? exType)
    {
        var (userId, isAdmin, isSuperAdmin, userBases) = GetUserContext();
        var ok = await _quoteService.UpdateRFQExTypeAsync(id, exType, userId, isAdmin);
        return ok ? Ok() : NotFound();
    }

    /// <summary>Save Yuan tax coefficient and exchange rate for a quote (base-3 customers).</summary>
    [HttpPatch("{id:long}/yuan-settings")]
    public async Task<IActionResult> UpdateYuanSettings(long id, [FromBody] UpdateQuoteYuanSettingsRequest request)
    {
        var ok = await _quoteService.UpdateYuanSettingsAsync(id, request.CoefYuan, request.ExchangeRateYuan);
        return ok ? Ok() : NotFound();
    }

    /// <summary>Delete a quote.</summary>
    [HttpDelete("{id:long}")]
    [Auditable("Quote", "Delete")]
    public async Task<IActionResult> Delete(long id)
    {
        var deleted = await _quoteService.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
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
