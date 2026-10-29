using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Identity.Entities;
using Procument.Module.Tasks.DTOs;
using Procument.Module.Tasks.Services;

namespace Procument.Module.Tasks.Controllers;

/// <summary>
/// Task Manager — one kanban board per user. Granted per user in Menu Access ("taskManager");
/// SuperAdmin always passes and may open every user's board (?userId=).
/// </summary>
[ApiController]
[Route("api/task-board")]
[Authorize]
public class TaskBoardController : ControllerBase
{
    public const string Feature = "taskManager";

    private readonly ITaskBoardService _service;
    private readonly DbContext _db;

    public TaskBoardController(ITaskBoardService service, DbContext db)
    {
        _service = service;
        _db = db;
    }

    private TaskBoardActor Actor => new(
        long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0,
        User.Identity?.Name ?? "System",
        User.IsInRole(UserRoles.SuperAdmin));

    private async Task<bool> HasAccessAsync()
    {
        if (User.IsInRole(UserRoles.SuperAdmin)) return true;
        var userName = User.Identity?.Name;
        return !string.IsNullOrEmpty(userName) && await _db.Set<MenuPermission>()
            .AnyAsync(p => p.Feature == Feature && p.UserName == userName);
    }

    /// <summary>Runs <paramref name="action"/> for a granted user and maps the service's exceptions to 403 / 404 / 400.</summary>
    private async Task<ActionResult> RunAsync(Func<Task<ActionResult>> action)
    {
        if (!await HasAccessAsync()) return Forbid();
        try
        {
            return await action();
        }
        catch (TaskBoardForbiddenException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ── Board ────────────────────────────────────────────────────────────────

    /// <summary>The caller's board, or (SuperAdmin) the board of <paramref name="userId"/>. Created with default columns on first open.</summary>
    [HttpGet]
    public Task<ActionResult> GetBoard([FromQuery] long? userId) =>
        RunAsync(async () => Ok(await _service.GetBoardAsync(Actor, userId)));

    /// <summary>SuperAdmin: every active user, to switch boards.</summary>
    [HttpGet("users")]
    public async Task<ActionResult<List<TaskBoardUserResponse>>> GetUsers()
    {
        if (!User.IsInRole(UserRoles.SuperAdmin)) return Forbid();
        return Ok(await _service.GetUsersAsync());
    }

    /// <summary>Nav badge: the caller's cards due today or overdue, outside Done columns (0 without access).</summary>
    [HttpGet("pending-count")]
    public async Task<ActionResult<int>> GetPendingCount()
    {
        if (!await HasAccessAsync()) return Ok(0);
        return Ok(await _service.GetPendingCountAsync(Actor.UserId));
    }

    /// <summary>References picker pages, newest first. type = Rfq | Quote | Invoice | PurchaseOrder | FinalInvoice.</summary>
    [HttpGet("ref-options")]
    public Task<ActionResult> SearchRefs([FromQuery] string type, [FromQuery] string? search, [FromQuery] int skip = 0, [FromQuery] int take = 30) =>
        RunAsync(async () => Ok(await _service.SearchRefsAsync(type, search, skip, take)));

    // ── Columns ──────────────────────────────────────────────────────────────

    [HttpPost("columns")]
    public Task<ActionResult> CreateColumn([FromBody] CreateTaskColumnRequest request) =>
        RunAsync(async () => Ok(await _service.CreateColumnAsync(Actor, request)));

    [HttpPut("columns/{id:long}")]
    public Task<ActionResult> UpdateColumn(long id, [FromBody] UpdateTaskColumnRequest request) =>
        RunAsync(async () => Ok(await _service.UpdateColumnAsync(Actor, id, request)));

    [HttpDelete("columns/{id:long}")]
    public Task<ActionResult> DeleteColumn(long id) =>
        RunAsync(async () =>
        {
            await _service.DeleteColumnAsync(Actor, id);
            return NoContent();
        });

    [HttpPut("columns/order")]
    public Task<ActionResult> ReorderColumns([FromBody] ReorderTaskColumnsRequest request) =>
        RunAsync(async () =>
        {
            await _service.ReorderColumnsAsync(Actor, request);
            return NoContent();
        });

    // ── Cards ────────────────────────────────────────────────────────────────

    [HttpPost("cards")]
    public Task<ActionResult> CreateCard([FromBody] CreateTaskCardRequest request) =>
        RunAsync(async () => Ok(await _service.CreateCardAsync(Actor, request)));

    [HttpGet("cards/{id:long}")]
    public Task<ActionResult> GetCard(long id) =>
        RunAsync(async () => Ok(await _service.GetCardAsync(Actor, id)));

    /// <summary>One field: title | description | priority | dueDate.</summary>
    [HttpPatch("cards/{id:long}")]
    public Task<ActionResult> UpdateCard(long id, [FromBody] UpdateTaskCardFieldRequest request) =>
        RunAsync(async () => Ok(await _service.UpdateCardFieldAsync(Actor, id, request)));

    [HttpPut("cards/{id:long}/move")]
    public Task<ActionResult> MoveCard(long id, [FromBody] MoveTaskCardRequest request) =>
        RunAsync(async () =>
        {
            await _service.MoveCardAsync(Actor, id, request);
            return NoContent();
        });

    [HttpPut("cards/{id:long}/tags")]
    public Task<ActionResult> SetTags(long id, [FromBody] SetTaskCardTagsRequest request) =>
        RunAsync(async () => Ok(await _service.SetTagsAsync(Actor, id, request.Names)));

    [HttpPut("cards/{id:long}/refs")]
    public Task<ActionResult> SetRefs(long id, [FromBody] SetTaskCardRefsRequest request) =>
        RunAsync(async () => Ok(await _service.SetRefsAsync(Actor, id, request.Refs)));

    [HttpPost("cards/{id:long}/comments")]
    public Task<ActionResult> AddComment(long id, [FromBody] AddTaskCommentRequest request) =>
        RunAsync(async () => Ok(await _service.AddCommentAsync(Actor, id, request.Text)));

    [HttpDelete("cards/{id:long}/comments/{activityId:long}")]
    public Task<ActionResult> DeleteComment(long id, long activityId) =>
        RunAsync(async () => Ok(await _service.DeleteCommentAsync(Actor, id, activityId)));

    /// <summary>Returns the board's tags afterwards (tags only this card used are gone).</summary>
    [HttpDelete("cards/{id:long}")]
    public Task<ActionResult> DeleteCard(long id) =>
        RunAsync(async () => Ok(await _service.DeleteCardAsync(Actor, id)));
}
