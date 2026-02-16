using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Identity.Entities;

namespace Procument.Module.Identity.Services;

public class PermissionService : IPermissionService
{
    private readonly DbContext _context;

    public PermissionService(DbContext context)
    {
        _context = context;
    }

    public async Task AddPermissionAsync(long userId, string entityName, string entityId, string permission)
    {
        var exists = await _context.Set<EntityPermission>()
            .AnyAsync(p => p.UserId == userId &&
                           p.EntityName == entityName &&
                           p.EntityId == entityId &&
                           p.Permission == permission);

        if (exists) return;

        var entityPermission = new EntityPermission
        {
            UserId = userId,
            EntityName = entityName,
            EntityId = entityId,
            Permission = permission
        };

        _context.Set<EntityPermission>().Add(entityPermission);
        await _context.SaveChangesAsync();
    }

    public async Task RemovePermissionAsync(long userId, string entityName, string entityId, string permission)
    {
        var entityPermission = await _context.Set<EntityPermission>()
            .FirstOrDefaultAsync(p => p.UserId == userId &&
                                      p.EntityName == entityName &&
                                      p.EntityId == entityId &&
                                      p.Permission == permission);

        if (entityPermission != null)
        {
            _context.Set<EntityPermission>().Remove(entityPermission);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> HasPermissionAsync(long userId, string entityName, string entityId, string permission)
    {
        return await _context.Set<EntityPermission>()
            .AnyAsync(p => p.UserId == userId &&
                           p.EntityName == entityName &&
                           p.EntityId == entityId &&
                           p.Permission == permission);
    }

    public async Task<List<EntityPermission>> GetPermissionsForEntityAsync(string entityName, string entityId)
    {
        return await _context.Set<EntityPermission>()
            .Include(p => p.User)
            .Where(p => p.EntityName == entityName && p.EntityId == entityId)
            .ToListAsync();
    }
}
