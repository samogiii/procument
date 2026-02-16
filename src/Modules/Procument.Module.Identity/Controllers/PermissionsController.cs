using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procument.Module.Identity.DTOs;
using Procument.Module.Identity.Entities;
using Procument.Module.Identity.Services;

namespace Procument.Module.Identity.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = UserRoles.Admin)]
public class PermissionsController : ControllerBase
{
    private readonly IPermissionService _permissionService;
    private readonly IAuthService _authService;

    public PermissionsController(IPermissionService permissionService, IAuthService authService)
    {
        _permissionService = permissionService;
        _authService = authService;
    }

    [HttpPost("assign")]
    public async Task<IActionResult> AssignPermission([FromBody] AssignPermissionRequest request)
    {
        var user = await _authService.GetUserByIdAsync(request.UserId);
        if (user == null) return NotFound("User not found");

        await _permissionService.AddPermissionAsync(request.UserId, request.EntityName, request.EntityId, request.Permission);
        return Ok();
    }

    [HttpPost("revoke")]
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
