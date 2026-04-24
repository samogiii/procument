using Procument.Module.Identity.Entities;

namespace Procument.Module.Identity.Services;

public interface IPermissionService
{
    Task AddPermissionAsync(long userId, string entityName, string entityId, string permission);
    Task RemovePermissionAsync(long userId, string entityName, string entityId, string permission);
    Task<bool> HasPermissionAsync(long userId, string entityName, string entityId, string permission);
    Task<List<EntityPermission>> GetPermissionsForEntityAsync(string entityName, string entityId);
    Task<List<EntityPermission>> GetPermissionsByEntityNameAsync(string entityName);
}
