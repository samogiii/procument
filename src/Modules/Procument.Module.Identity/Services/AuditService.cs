using Microsoft.EntityFrameworkCore;

using Procument.Shared.Entities;

namespace Procument.Module.Identity.Services;

public class AuditService : IAuditService
{
    private readonly DbContext _context;

    public AuditService(DbContext context)
    {
        _context = context;
    }

    public async Task LogAsync(long? userId, string action, string entityName, string entityId, string? details = null)
    {
        var log = new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Details = details,
            Timestamp = DateTime.UtcNow
            // IPAddress could be retrieved from IHttpContextAccessor if needed, but keeping it simple for now or passed in details
        };

        _context.Set<AuditLog>().Add(log);
        await _context.SaveChangesAsync();
    }

    public async Task<List<AuditLog>> GetLogsAsync(string entityName, string entityId)
    {
        return await _context.Set<AuditLog>()
            .Where(l => l.EntityName == entityName && l.EntityId == entityId)
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();
    }

    public async Task<List<AuditLog>> GetAllLogsAsync(int limit = 100)
    {
        return await _context.Set<AuditLog>()
            .OrderByDescending(l => l.Timestamp)
            .Take(limit)
            .ToListAsync();
    }
}
