using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procument.Module.RFQ.DTOs;
using Procument.Module.RFQ.Entities;
using Procument.Module.RFQ.Services;
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

    public RFQsController(IRFQService rfqService, IFinalInvoiceLockGuard lockGuard, DbContext db)
    {
        _rfqService = rfqService;
        _lockGuard = lockGuard;
        _db = db;
    }

    /// <summary>Create a new RFQ. Auto-creates customer and part numbers if they don't exist.</summary>
    [HttpPost]
    public async Task<ActionResult<RFQResponse>> Create([FromBody] CreateRFQRequest request)
    {
        var result = await _rfqService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Get all RFQs.</summary>
    [HttpGet]
    public async Task<ActionResult<List<RFQResponse>>> GetAll()
    {
        var (userId, isAdmin) = GetUserContext();
        var result = await _rfqService.GetAllAsync(userId, isAdmin);
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

        var success = await _rfqService.UpdateStatusAsync(id, request.Status);
        return success ? Ok() : NotFound();
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