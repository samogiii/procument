using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Procument.Shared.Entities;
using Procument.Shared.Services;
using WebPush;

namespace Procument.API.Services;

public class NotificationService : INotificationService
{
    private readonly DbContext _db;
    private readonly VapidDetails? _vapid;

    public NotificationService(DbContext db, IConfiguration config)
    {
        _db = db;

        var pub = config["Vapid:PublicKey"];
        var priv = config["Vapid:PrivateKey"];
        var subj = config["Vapid:Subject"];
        if (!string.IsNullOrWhiteSpace(pub) && !string.IsNullOrWhiteSpace(priv) && !string.IsNullOrWhiteSpace(subj))
            _vapid = new VapidDetails(subj, pub, priv);
    }

    // ── Create for a single user ──────────────────────────────────────────────

    public async Task CreateAsync(long userId, string type, string entityName, long entityId, string entityNumber,
        string message, long? triggeredByUserId = null, string? triggeredByUserName = null)
    {
        var n = new Notification
        {
            UserId = userId,
            Type = type,
            EntityName = entityName,
            EntityId = entityId,
            EntityNumber = entityNumber,
            Message = message,
            TriggeredByUserId = triggeredByUserId,
            TriggeredByUserName = triggeredByUserName,
            CreatedAt = DateTime.UtcNow
        };
        _db.Set<Notification>().Add(n);
        await _db.SaveChangesAsync();
        await SendPushAsync(userId, type, message, entityName, entityId);
    }

    // ── Create for an explicit list of users ─────────────────────────────────

    public async Task CreateForUsersAsync(IEnumerable<long> userIds, string type, string entityName, long entityId,
        string entityNumber, string message, long? triggeredByUserId = null, string? triggeredByUserName = null)
    {
        var list = userIds.Distinct().ToList();
        if (list.Count == 0) return;

        foreach (var uid in list)
        {
            _db.Set<Notification>().Add(new Notification
            {
                UserId = uid,
                Type = type,
                EntityName = entityName,
                EntityId = entityId,
                EntityNumber = entityNumber,
                Message = message,
                TriggeredByUserId = triggeredByUserId,
                TriggeredByUserName = triggeredByUserName,
                CreatedAt = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync();

        foreach (var uid in list)
            await SendPushAsync(uid, type, message, entityName, entityId);
    }

    // ── Create for all active admins ─────────────────────────────────────────

    public async Task CreateForAllAdminsAsync(string type, string entityName, long entityId, string entityNumber,
        string message, long? triggeredByUserId = null, string? triggeredByUserName = null)
    {
        var adminIds = await _db.Set<Module.Identity.Entities.User>()
            .Where(u => (u.Role == "Admin" || u.Role == "SuperAdmin") && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync();

        await CreateForUsersAsync(adminIds, type, entityName, entityId, entityNumber, message,
            triggeredByUserId, triggeredByUserName);
    }

    // ── Read / dismiss ────────────────────────────────────────────────────────

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
                TriggeredByUserName = n.TriggeredByUserName,
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
                TriggeredByUserName = n.TriggeredByUserName,
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
        await _db.Set<Notification>()
            .Where(n => n.Id == notificationId && n.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
    }

    public async Task MarkAllReadAsync(long userId)
    {
        await _db.Set<Notification>()
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
    }

    public async Task DismissAsync(long userId, long notificationId)
    {
        await _db.Set<Notification>()
            .Where(n => n.Id == notificationId && n.UserId == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.IsDismissed, true)
                .SetProperty(n => n.IsRead, true));
    }

    public async Task DismissAllRejectionsAsync(long userId)
    {
        await _db.Set<Notification>()
            .Where(n => n.UserId == userId && n.Type == "Rejection" && !n.IsDismissed)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.IsDismissed, true)
                .SetProperty(n => n.IsRead, true));
    }

    // ── Push subscription management ─────────────────────────────────────────

    public async Task SubscribePushAsync(long userId, string endpoint, string p256dh, string auth)
    {
        var existing = await _db.Set<UserPushSubscription>()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Endpoint == endpoint);
        if (existing == null)
        {
            _db.Set<UserPushSubscription>().Add(new UserPushSubscription
            {
                UserId = userId,
                Endpoint = endpoint,
                P256dh = p256dh,
                Auth = auth,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }
    }

    public async Task UnsubscribePushAsync(long userId, string endpoint)
    {
        var sub = await _db.Set<UserPushSubscription>()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Endpoint == endpoint);
        if (sub != null) { _db.Set<UserPushSubscription>().Remove(sub); await _db.SaveChangesAsync(); }
    }

    public string? GetVapidPublicKey() => _vapid?.PublicKey;

    // ── Internal push sender ─────────────────────────────────────────────────

    private async Task SendPushAsync(long userId, string type, string message, string entityName, long entityId)
    {
        if (_vapid == null) return;

        var subs = await _db.Set<UserPushSubscription>()
            .Where(s => s.UserId == userId)
            .ToListAsync();

        if (subs.Count == 0) return;

        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            title = "Procument",
            body = message,
            url = $"/{entityName.ToLower().Replace("tracknumber", "shipping/track-numbers")}/{entityId}"
        });

        var client = new WebPushClient();
        var stale = new List<UserPushSubscription>();

        foreach (var s in subs)
        {
            try
            {
                var sub = new PushSubscription(s.Endpoint, s.P256dh, s.Auth);
                await client.SendNotificationAsync(sub, payload, _vapid);
            }
            catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone
                                               || ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                stale.Add(s);
            }
            catch { /* ignore transient errors */ }
        }

        if (stale.Count > 0)
        {
            _db.Set<UserPushSubscription>().RemoveRange(stale);
            await _db.SaveChangesAsync();
        }
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
    public string? TriggeredByUserName { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
