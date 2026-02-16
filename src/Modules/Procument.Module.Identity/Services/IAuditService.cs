namespace Procument.Module.Identity.Services;

public interface IAuditService
{
    Task LogAsync(long? userId, string action, string entityName, string entityId, string? details = null);
    Task<List<Shared.Entities.AuditLog>> GetLogsAsync(string entityName, string entityId);
}
