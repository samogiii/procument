using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Sales.DTOs;
using Procument.Module.Sales.Entities;
using Procument.Module.Sales.Services;
using Procument.Module.Identity.Entities;
using Procument.Shared.Entities;
using System.Security.Claims;

using Procument.Shared.Audit;

namespace Procument.Module.Sales.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Expert")]
public class QuotesController : ControllerBase
{
    private readonly IQuoteService _quoteService;
    private readonly DbContext _db;

    public QuotesController(IQuoteService quoteService, DbContext db)
    {
        _quoteService = quoteService;
        _db = db;
    }

    /// <summary>Get all quotes (paginated).</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<QuoteResponse>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? status = null)
    {
        var (userId, isAdmin) = GetUserContext();
        var result = await _quoteService.GetAllAsync(page, pageSize, userId, isAdmin, status);
        return Ok(result);
    }

    /// <summary>Get all quotes for an RFQ.</summary>
    [HttpGet("by-rfq/{rfqId:long}")]
    public async Task<ActionResult<List<QuoteResponse>>> GetByRFQ(long rfqId)
    {
        var (userId, isAdmin) = GetUserContext();
        var result = await _quoteService.GetByRFQIdAsync(rfqId, userId, isAdmin);
        return Ok(result);
    }

    /// <summary>Get a quote by ID.</summary>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<QuoteResponse>> GetById(long id)
    {
        var (userId, isAdmin) = GetUserContext();
        var result = await _quoteService.GetByIdAsync(id, userId, isAdmin);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Create a new quote from selected procurement records.</summary>
    [HttpPost]
    [Auditable("Quote", "Create", CaptureBody = true)]
    public async Task<ActionResult<QuoteResponse>> Create([FromBody] CreateQuoteRequest request)
    {
        var (userId, isAdmin) = GetUserContext();
        if (userId == 0) return Unauthorized("User ID not found in token.");

        var result = await _quoteService.CreateAsync(request, userId);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Update quote status.</summary>
    [HttpPatch("{id:long}/status")]
    [Auditable("Quote", "UpdateStatus", CaptureBody = true)]
    public async Task<IActionResult> UpdateStatus(long id, [FromBody] UpdateQuoteStatusRequest request)
    {
        var (userId, isAdmin) = GetUserContext();
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
            var adminIds = await _db.Set<User>().Where(u => u.Role == "Admin" && u.IsActive).Select(u => u.Id).ToListAsync();
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
        var (userId, isAdmin) = GetUserContext();
        var result = await _quoteService.UpdateAsync(id, request, userId, isAdmin);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Update quote Type.</summary>
    [HttpPatch("{id:long}/types")]
    [Auditable("Quote", "UpdateStatus", CaptureBody = true)]
    public async Task<IActionResult> UpdateQuoteType(long id, [FromBody] QuoteTypeDTO request)
    {
        var (userId, isAdmin) = GetUserContext();
        var success = await _quoteService.UpdateQuoteTypeAsync(id, request.QuoteType,request.TypeAdditional, userId, isAdmin);
        return success ? Ok() : NotFound();
    }

    /// <summary>Delete a quote.</summary>
    [HttpDelete("{id:long}")]
    [Auditable("Quote", "Delete")]
    public async Task<IActionResult> Delete(long id)
    {
        var deleted = await _quoteService.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
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
