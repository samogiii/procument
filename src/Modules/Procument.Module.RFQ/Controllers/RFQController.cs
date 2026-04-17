using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Identity.Entities;
using Procument.Module.Identity.Services;
using Procument.Module.RFQ.DTOs;
using Procument.Module.RFQ.Entities;
using Procument.Module.RFQ.Services;
using Procument.Shared.DTOs;
using Procument.Shared.Entities;
using Procument.Shared.Services;

namespace Procument.Module.RFQ.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Expert")]
public class RFQsController : ControllerBase
{
    private readonly IRFQService _rfqService;
    private readonly IFinalInvoiceLockGuard _lockGuard;
    private readonly DbContext _db;
    private readonly IAuditService _auditService;

    public RFQsController(IRFQService rfqService, IFinalInvoiceLockGuard lockGuard, DbContext db, IAuditService auditService)
    {
        _rfqService = rfqService;
        _lockGuard = lockGuard;
        _db = db;
        _auditService = auditService;
    }

    /// <summary>Create a new RFQ. Auto-creates customer and part numbers if they don't exist.</summary>
    [HttpPost]
    public async Task<ActionResult<RFQResponse>> Create([FromBody] CreateRFQRequest request)
    {
        var result = await _rfqService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Get all RFQs (paginated).</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<RFQListItem>>> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 200,
        [FromQuery] string? search = null,
        [FromQuery] string[]? status = null,
        [FromQuery] string? pnSearch = null,
        [FromQuery] long[]? userId_filter = null,
        [FromQuery] string? customer = null)
    {
        var pq = new PageQuery { Page = page, PageSize = pageSize, Search = search };
        var (userId, isAdmin) = GetUserContext();
        var result = await _rfqService.GetAllAsync(userId, isAdmin, pq, status, pnSearch, userId_filter, customer);
        return Ok(result);
    }

    /// <summary>Get RFQ by ID.</summary>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<RFQResponse>> GetById(long id)
    {
        var (userId, isAdmin) = GetUserContext();
        var result = await _rfqService.GetByIdAsync(id, userId, isAdmin);

        if (result == null)
        {
            // If service returns null, it could be 404 or 403 (filtered out). 
            // Returning NotFound covers both securely.
            return NotFound();
        }

        return Ok(result);
    }

    private (long userId, bool isAdmin) GetUserContext()
    {
        var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        long userId = 0;
        if (idClaim != null && long.TryParse(idClaim.Value, out var id))
        {
            userId = id;
        }
        bool isAdmin = User.IsInRole("Admin");
        return (userId, isAdmin);
    }

    /// <summary>Update an RFQ item's fields (Alt, Qty, Condition).</summary>
    [HttpPut("items/{itemId:long}")]
    public async Task<ActionResult<RFQItemResponse>> UpdateItem(long itemId, [FromBody] UpdateRFQItemRequest request)
    {
        var result = await _rfqService.UpdateItemAsync(itemId, request);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Add a new item to an existing RFQ. Creates part number if it doesn't exist.</summary>
    [HttpPost("{id:long}/items")]
    public async Task<ActionResult<RFQItemResponse>> AddItem(long id, [FromBody] AddRFQItemRequest request)
    {
        var result = await _rfqService.AddItemAsync(id, request);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Delete an RFQ item if it has no linked quote items. Admin only.</summary>
    [HttpDelete("items/{itemId:long}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteItem(long itemId)
    {
        var item =await  _rfqService.DeleteRFQItem(itemId);
        return NoContent();
    }

    /// <summary>Update the status of an RFQ.</summary>
    [HttpPatch("{id:long}/status")]
    public async Task<IActionResult> UpdateStatus(long id, [FromBody] UpdateStatusRequest request)
    {
        if (await _lockGuard.IsRfqLocked(id))
            return BadRequest(new { message = "This RFQ is locked because a Final Invoice has been created." });

        // Get current RFQ for audit logging
        var rfq = await _db.Set<RFQHeader>().FindAsync(id);
        if (rfq == null) return NotFound();

        var (userId, isAdmin) = GetUserContext();
        var userName = await _db.Set<User>().Where(u => u.Id == userId).Select(u => u.Name).FirstOrDefaultAsync() ?? "System";

        string targetStatus = request.Status;
        string? noQuoteReason = request.NoQuoteReason;

        // Special logic for "No Quote" requested by non-admin
        if (targetStatus == "No Quote" && !isAdmin)
        {
            targetStatus = "Waiting For Admin";
        }

        var oldStatus = rfq.Status;
        var success = await _rfqService.UpdateStatusAsync(id, targetStatus, noQuoteReason);

        if (success)
        {
            if (targetStatus == "No Quote")
            {
                await _auditService.LogRFQNoQuoteAsync(userId, userName, id, rfq.Name, noQuoteReason);
            }
            else if (targetStatus == "Waiting For Admin")
            {
                await _auditService.LogRFQStatusChangedAsync(userId, userName, id, rfq.Name, oldStatus, "Waiting For Admin", $"Requested No Quote. Reason: {noQuoteReason}");
            }
            else
            {
                await _auditService.LogRFQStatusChangedAsync(userId, userName, id, rfq.Name, oldStatus, targetStatus, noQuoteReason);
            }
            return Ok();
        }

        return NotFound();
    }

    /// <summary>Admin reverts a 'No Quote' RFQ back to Open and unassigns all users.</summary>
    [HttpPost("{id:long}/revert-no-quote")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RevertNoQuote(long id)
    {
        var rfq = await _db.Set<RFQHeader>().FindAsync(id);
        if (rfq == null) return NotFound();

        var (userId, _) = GetUserContext();
        var userName = await _db.Set<User>().Where(u => u.Id == userId).Select(u => u.Name).FirstOrDefaultAsync() ?? "Admin";

        var oldStatus = rfq.Status;

        var success = await _rfqService.UpdateStatusAsync(id, "Open", null);
        if (!success) return BadRequest();

        var permissions = await _db.Set<EntityPermission>()
            .Where(p => p.EntityName == "RFQ" && p.EntityId == id.ToString())
            .ToListAsync();
        if (permissions.Count > 0)
        {
            _db.Set<EntityPermission>().RemoveRange(permissions);
            await _db.SaveChangesAsync();
        }

        await _auditService.LogRFQStatusChangedAsync(userId, userName, id, rfq.Name, oldStatus, "Open", "Reverted from No Quote; all users unassigned");

        return Ok();
    }

    /// <summary>Admin accepts a 'No Quote' request.</summary>
    [HttpPost("{id:long}/accept-no-quote")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AcceptNoQuote(long id)
    {
        var rfq = await _db.Set<RFQHeader>().FindAsync(id);
        if (rfq == null) return NotFound();

        var (userId, _) = GetUserContext();
        var userName = await _db.Set<User>().Where(u => u.Id == userId).Select(u => u.Name).FirstOrDefaultAsync() ?? "Admin";

        var success = await _rfqService.UpdateStatusAsync(id, "No Quote", rfq.NoQuoteReason);
        if (success)
        {
            await _auditService.LogRFQNoQuoteAsync(userId, userName, id, rfq.Name, rfq.NoQuoteReason);
            return Ok();
        }
        return BadRequest();
    }

    /// <summary>Admin rejects a 'No Quote' request.</summary>
    [HttpPost("{id:long}/reject-no-quote")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RejectNoQuote(long id, [FromBody] RejectNoQuoteRequest request)
    {
        var rfq = await _db.Set<RFQHeader>().FindAsync(id);
        if (rfq == null) return NotFound();

        var (userId, _) = GetUserContext();
        var userName = await _db.Set<User>().Where(u => u.Id == userId).Select(u => u.Name).FirstOrDefaultAsync() ?? "Admin";

        var oldReason = rfq.NoQuoteReason;

        // Revert to In Progress with rejection note
        var success = await _rfqService.UpdateStatusAsync(id, "In Progress", null, request.RejectionNote);
        if (success)
        {
            await _auditService.LogRFQNoQuoteRejectedAsync(userId, userName, id, rfq.Name, oldReason, request.RejectionNote);

            // ── Notification: Create notification for creator and assigned users ──
            var usersToNotify = await _db.Set<EntityPermission>()
                .Where(p => p.EntityName == "RFQ" && p.EntityId == id.ToString())
                .Select(p => p.UserId)
                .ToListAsync();
            
            if (rfq.UserId.HasValue) usersToNotify.Add(rfq.UserId.Value);
            usersToNotify = usersToNotify.Distinct().Where(u => u != userId).ToList();

            var message = $"No Quote request for RFQ #{id} ({rfq.Name}) was rejected.";
            if (!string.IsNullOrEmpty(request.RejectionNote))
            {
                message += $" Admin note: {request.RejectionNote}";
            }

            foreach (var targetUserId in usersToNotify)
            {
                _db.Set<Notification>().Add(new Notification
                {
                    UserId = targetUserId,
                    Type = "Rejection",
                    EntityName = "RFQ",
                    EntityId = id,
                    EntityNumber = rfq.Name,
                    Message = message,
                    RejectionNote = request.RejectionNote,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });

                // Also mark RFQ as unread
                var readRecord = await _db.Set<RFQUserRead>()
                    .FirstOrDefaultAsync(r => r.RFQId == id && r.UserId == targetUserId);

                if (readRecord != null)
                {
                    readRecord.IsRead = false;
                    readRecord.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    _db.Set<RFQUserRead>().Add(new RFQUserRead
                    {
                        RFQId = id,
                        UserId = targetUserId,
                        IsRead = false,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }

            await _db.SaveChangesAsync();
            return Ok();
        }
        return BadRequest();
    }

    /// <summary>Update the ExType of an RFQ.</summary>
    [HttpPatch("{id:long}/extype")]
    public async Task<IActionResult> UpdateExType(long id, [FromBody] UpdateExTypeRequest request)
    {
        var success = await _rfqService.UpdateExTypeAsync(id, request.ExType);
        return success ? Ok() : NotFound();
    }

    /// <summary>Update the Notes of an RFQ.</summary>
    [HttpPatch("{id:long}/notes")]
    public async Task<IActionResult> UpdateNotes(long id, [FromBody] UpdateRFQNotesRequest request)
    {
        var success = await _rfqService.UpdateNotesAsync(id, request.Notes);
        return success ? Ok() : NotFound();
    }

    /// <summary>Update the Name of an RFQ.</summary>
    [HttpPatch("{id:long}/name")]
    public async Task<IActionResult> UpdateName(long id, [FromBody] UpdateRFQNameRequest request)
    {
        var success = await _rfqService.UpdateNameAsync(id, request.Name);
        return success ? Ok() : NotFound();
    }

    /// <summary>Update the LeadTime (Deadline) of an RFQ.</summary>
    [HttpPatch("{id:long}/leadtime")]
    public async Task<IActionResult> UpdateLeadTime(long id, [FromBody] UpdateRFQLeadTimeRequest request)
    {
        var success = await _rfqService.UpdateLeadTimeAsync(id, request.LeadTime);
        return success ? Ok() : NotFound();
    }

    /// <summary>Mark an RFQ as read for the current user.</summary>
    [HttpPatch("{id:long}/mark-read")]
    public async Task<IActionResult> MarkRead(long id)
    {
        var (userId, _) = GetUserContext();
        var record = await _db.Set<RFQUserRead>()
            .FirstOrDefaultAsync(r => r.RFQId == id && r.UserId == userId);

        if (record != null)
        {
            record.IsRead = true;
            record.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return Ok();
    }

    /// <summary>Mark an RFQ as unread for the current user.</summary>
    [HttpPatch("{id:long}/mark-unread")]
    public async Task<IActionResult> MarkUnread(long id)
    {
        var (userId, _) = GetUserContext();
        var record = await _db.Set<RFQUserRead>()
            .FirstOrDefaultAsync(r => r.RFQId == id && r.UserId == userId);

        if (record != null)
        {
            record.IsRead = false;
            record.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.Set<RFQUserRead>().Add(new RFQUserRead
            {
                RFQId = id,
                UserId = userId,
                IsRead = false,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
        return Ok();
    }
}
///TODO Fix Service later