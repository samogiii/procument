namespace Procument.Shared.Services;

/// <summary>
/// Shared interface so Purchasing module (and others) can fire notifications
/// without depending on Procument.API.
/// </summary>
public interface INotificationService
{
    Task CreateAsync(long userId, string type, string entityName, long entityId, string entityNumber, string message,
        long? triggeredByUserId = null, string? triggeredByUserName = null);

    Task CreateForUsersAsync(IEnumerable<long> userIds, string type, string entityName, long entityId,
        string entityNumber, string message, long? triggeredByUserId = null, string? triggeredByUserName = null);

    Task CreateForAllAdminsAsync(string type, string entityName, long entityId, string entityNumber, string message,
        long? triggeredByUserId = null, string? triggeredByUserName = null);
}
