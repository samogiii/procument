using Microsoft.EntityFrameworkCore;
using Procument.Module.Identity.Entities;
using Procument.Module.Tasks.DTOs;
using Procument.Module.Tasks.Entities;
using TaskStatus = Procument.Module.Tasks.Entities.TaskStatus;

namespace Procument.Module.Tasks.Services;

public interface ITaskService
{
    Task<TaskResponse> CreateAsync(CreateTaskRequest request, string creatorCode);
    Task<List<TaskResponse>> GetAllAsync(string? userCode, bool isAdmin);
    Task<bool> UpdateStatusAsync(long id, TaskStatus status, string userCode, bool isAdmin);
    Task<bool> DeleteAsync(long id);
    Task<int> GetPendingCountAsync(string? userCode, bool isAdmin);
}

public class TaskService : ITaskService
{
    private readonly DbContext _db;

    public TaskService(DbContext db)
    {
        _db = db;
    }

    public async Task<TaskResponse> CreateAsync(CreateTaskRequest request, string creatorCode)
    {
        var assignedToUser = await _db.Set<User>()
            .FirstOrDefaultAsync(u => u.Name == request.AssignedTo);

        var task = new TaskItem
        {
            Title = request.Title,
            Description = request.Description,
            AssignedTo = request.AssignedTo,
            CreatedByCode = creatorCode,
            Status = TaskStatus.NotStarted,
            AssignedToUserId = assignedToUser?.Id,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _db.Set<TaskItem>().Add(task);
        await _db.SaveChangesAsync();

        return MapToResponse(task);
    }

    public async Task<List<TaskResponse>> GetAllAsync(string? userCode, bool isAdmin)
    {
        var query = _db.Set<TaskItem>()
            .Include(t => t.AssignedToUser)
            .Where(t => t.IsActive);

        if (!isAdmin && !string.IsNullOrEmpty(userCode))
        {
            query = query.Where(t => t.AssignedTo == userCode);
        }

        var tasks = await query.OrderByDescending(t => t.CreatedAt).ToListAsync();
        return tasks.Select(MapToResponse).ToList();
    }

    public async Task<bool> UpdateStatusAsync(long id, TaskStatus status, string userCode, bool isAdmin)
    {
        var task = await _db.Set<TaskItem>().FindAsync(id);
        if (task == null) return false;

        // Only assigned user or admin can move tasks
        if (!isAdmin && task.AssignedTo != userCode) return false;

        task.Status = status;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var task = await _db.Set<TaskItem>().FindAsync(id);
        if (task == null) return false;

        task.IsActive = false; // Soft delete
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<int> GetPendingCountAsync(string? userCode, bool isAdmin)
    {
        var query = _db.Set<TaskItem>()
            .Where(t => t.IsActive && t.Status == TaskStatus.NotStarted);

        if (!isAdmin && !string.IsNullOrEmpty(userCode))
        {
            query = query.Where(t => t.AssignedTo == userCode);
        }

        return await query.CountAsync();
    }

    private static TaskResponse MapToResponse(TaskItem task) => new()
    {
        Id = task.Id,
        Title = task.Title,
        Description = task.Description,
        AssignedTo = task.AssignedTo,
        CreatedByCode = task.CreatedByCode,
        Status = task.Status,
        CreatedAt = task.CreatedAt,
        AssignedToUserName = task.AssignedToUser?.Name
    };
}
