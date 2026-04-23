using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Identity.DTOs;
using Procument.Module.Identity.Entities;
using Procument.Module.Identity.Services;

using Procument.Shared.Audit;
using Procument.Shared.Entities;

namespace Procument.Module.Identity.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = UserRoles.Admin + "," + UserRoles.SuperAdmin)]
public class PermissionsController : ControllerBase
{
    private readonly IPermissionService _permissionService;
    private readonly IAuthService _authService;
    private readonly DbContext _db;

    public PermissionsController(IPermissionService permissionService, IAuthService authService, DbContext db)
    {
        _permissionService = permissionService;
        _authService = authService;
        _db = db;
    }

    [HttpPost("assign")]
    [Auditable("Permission", "Assign", CaptureBody = true)]
    public async Task<IActionResult> AssignPermission([FromBody] AssignPermissionRequest request)
    {
        var user = await _authService.GetUserByIdAsync(request.UserId);
        if (user == null) return NotFound("User not found");

        await _permissionService.AddPermissionAsync(request.UserId, request.EntityName, request.EntityId, request.Permission);

        // Mark RFQ as unread for the assigned user
        if (request.EntityName == "RFQ" && long.TryParse(request.EntityId, out var rfqId))
        {
            try
            {
                var existing = await _db.Set<RFQUserRead>()
                    .FirstOrDefaultAsync(r => r.RFQId == rfqId && r.UserId == request.UserId);

                if (existing != null)
                {
                    existing.IsRead = false;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    _db.Set<RFQUserRead>().Add(new RFQUserRead
                    {
                        RFQId = rfqId,
                        UserId = request.UserId,
                        IsRead = false,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                await _db.SaveChangesAsync();

                // Automatically change status to In Progress if currently Open or Draft
                await _db.Database.ExecuteSqlInterpolatedAsync($"UPDATE RFQs SET Status = 'In Progress' WHERE Id = {rfqId} AND Status IN ('Open', 'Draft')");
            }
            catch { /* Don't fail assignment if unread marking fails */ }
        }

        return Ok();
    }

    [HttpPost("revoke")]
    [Auditable("Permission", "Revoke", CaptureBody = true)]
    public async Task<IActionResult> RevokePermission([FromBody] AssignPermissionRequest request)
    {
        await _permissionService.RemovePermissionAsync(request.UserId, request.EntityName, request.EntityId, request.Permission);
        return Ok();
    }

    [HttpGet("{entityName}/{entityId}")]
    public async Task<ActionResult<List<EntityPermission>>> GetPermissions(string entityName, string entityId)
    {
        var permissions = await _permissionService.GetPermissionsForEntityAsync(entityName, entityId);
        return Ok(permissions);
    }
}
