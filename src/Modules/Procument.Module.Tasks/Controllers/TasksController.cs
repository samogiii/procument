using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procument.Module.Tasks.DTOs;
using Procument.Module.Tasks.Services;
using System.Security.Claims;

namespace Procument.Module.Tasks.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly ITaskService _taskService;

    public TasksController(ITaskService taskService)
    {
        _taskService = taskService;
    }

    [HttpGet]
    public async Task<ActionResult<List<TaskResponse>>> GetAll()
    {
        var userCode = User.Identity?.Name; // Assuming Name claim stores the 'GHS', etc.
        var isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
        
        var tasks = await _taskService.GetAllAsync(userCode, isAdmin);
        return Ok(tasks);
    }

    [HttpGet("pending-count")]
    public async Task<ActionResult<int>> GetPendingCount()
    {
        var userCode = User.Identity?.Name;
        var isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
        var count = await _taskService.GetPendingCountAsync(userCode, isAdmin);
        return Ok(count);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<TaskResponse>> Create([FromBody] CreateTaskRequest request)
    {
        var creatorCode = User.Identity?.Name ?? "System";
        var task = await _taskService.CreateAsync(request, creatorCode);
        return Ok(task);
    }

    [HttpPut("{id:long}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<TaskResponse>> Update(long id, [FromBody] UpdateTaskRequest request)
    {
        var task = await _taskService.UpdateAsync(id, request);
        if (task == null) return NotFound();
        return Ok(task);
    }

    [HttpPatch("{id:long}/status")]
    public async Task<ActionResult> UpdateStatus(long id, [FromBody] UpdateTaskStatusRequest request)
    {
        var userCode = User.Identity?.Name ?? "";
        var isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");

        var success = await _taskService.UpdateStatusAsync(id, request.Status, userCode, isAdmin);
        if (!success) return Forbid();

        return NoContent();
    }

    [HttpDelete("{id:long}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult> Delete(long id)
    {
        var success = await _taskService.DeleteAsync(id);
        if (!success) return NotFound();
        return NoContent();
    }
}
