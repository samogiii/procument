using Microsoft.EntityFrameworkCore;
using Procument.Shared.Entities;

namespace Procument.API.Services;

public interface INotificationService
{
    Task CreateAsync(long userId, string type, string entityName, long entityId, string entityNumber, string message, string? rejectionNote = null);
    Task CreateForAllAdminsAsync(string type, string entityName, long entityId, string entityNumber, string message);
    Task<List<NotificationDto>> GetForUserAsync(long userId);
    Task<List<NotificationDto>> GetUndismissedRejectionsAsync(long userId);
    Task<int> GetUnreadCountAsync(long userId);
    Task MarkReadAsync(long userId, long notificationId);
    Task DismissAsync(long userId, long notificationId);
    Task DismissAllRejectionsAsync(long userId);
}

public class NotificationService : INotificationService
{
    private readonly DbContext _db;

    public NotificationService(DbContext db)
    {
        _db = db;
    }

    public async Task CreateAsync(long userId, string type, string entityName, long entityId, string entityNumber, string message, string? rejectionNote = null)
    {
        var notification = new Notification
        {
            UserId = userId,
            Type = type,
            EntityName = entityName,
            EntityId = entityId,
            EntityNumber = entityNumber,
            Message = message,
            RejectionNote = rejectionNote,
            CreatedAt = DateTime.UtcNow
        };
        _db.Set<Notification>().Add(notification);
        await _db.SaveChangesAsync();
    }

    public async Task CreateForAllAdminsAsync(string type, string entityName, long entityId, string entityNumber, string message)
    {
        var adminIds = await _db.Set<Module.Identity.Entities.User>()
            .Where(u => u.Role == "Admin" && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync();

        foreach (var adminId in adminIds)
        {
            _db.Set<Notification>().Add(new Notification
            {
                UserId = adminId,
                Type = type,
                EntityName = entityName,
                EntityId = entityId,
                EntityNumber = entityNumber,
                Message = message,
                CreatedAt = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync();
    }

    public async Task<List<NotificationDto>> GetForUserAsync(long userId)
    {
        return await _db.Set<Notification>()
            .Where(n => n.UserId == userId && !n.IsDismissed)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                Type = n.Type,
                EntityName = n.EntityName,
                EntityId = n.EntityId,
                EntityNumber = n.EntityNumber,
                Message = n.Message,
                RejectionNote = n.RejectionNote,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<List<NotificationDto>> GetUndismissedRejectionsAsync(long userId)
    {
        return await _db.Set<Notification>()
            .Where(n => n.UserId == userId && n.Type == "Rejection" && !n.IsDismissed)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                Type = n.Type,
                EntityName = n.EntityName,
                EntityId = n.EntityId,
                EntityNumber = n.EntityNumber,
                Message = n.Message,
                RejectionNote = n.RejectionNote,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<int> GetUnreadCountAsync(long userId)
    {
        return await _db.Set<Notification>()
            .CountAsync(n => n.UserId == userId && !n.IsRead && !n.IsDismissed);
    }

    public async Task MarkReadAsync(long userId, long notificationId)
    {
        var n = await _db.Set<Notification>().FirstOrDefaultAsync(x => x.Id == notificationId && x.UserId == userId);
        if (n != null)
        {
            n.IsRead = true;
            await _db.SaveChangesAsync();
        }
    }

    public async Task DismissAsync(long userId, long notificationId)
    {
        var n = await _db.Set<Notification>().FirstOrDefaultAsync(x => x.Id == notificationId && x.UserId == userId);
        if (n != null)
        {
            n.IsDismissed = true;
            n.IsRead = true;
            await _db.SaveChangesAsync();
        }
    }

    public async Task DismissAllRejectionsAsync(long userId)
    {
        var rejections = await _db.Set<Notification>()
            .Where(n => n.UserId == userId && n.Type == "Rejection" && !n.IsDismissed)
            .ToListAsync();

        foreach (var r in rejections)
        {
            r.IsDismissed = true;
            r.IsRead = true;
        }
        await _db.SaveChangesAsync();
    }
}

public class NotificationDto
{
    public long Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public long EntityId { get; set; }
    public string EntityNumber { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? RejectionNote { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
