using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procument.Module.Identity.Services;
using Procument.Shared.Entities;

namespace Procument.Module.Identity.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AuditController : ControllerBase
{
    private readonly IAuditService _auditService;

    public AuditController(IAuditService auditService)
    {
        _auditService = auditService;
    }

    [HttpGet("{entityName}/{entityId}")]
    public async Task<ActionResult<List<AuditLog>>> GetLogs(string entityName, string entityId)
    {
        var logs = await _auditService.GetLogsAsync(entityName, entityId);
        return Ok(logs);
    }

    [HttpGet]
    public async Task<ActionResult<List<AuditLog>>> GetAllLogs(
        [FromQuery] string? entityName = null,
        [FromQuery] string? entityId = null,
        [FromQuery] string? actionCategory = null,
        [FromQuery] int limit = 50000)
    {
        try
        {
            // If specific entity filtering is requested, use GetBusinessLogsAsync
            // Note: actionCategory filter only works if migration has been applied
            if (!string.IsNullOrEmpty(entityName) || !string.IsNullOrEmpty(entityId))
            {
                var logs = await _auditService.GetBusinessLogsAsync(entityName, entityId, actionCategory, limit);
                return Ok(logs);
            }

            // Otherwise get all logs
            var allLogs = await _auditService.GetAllLogsAsync(limit);
            return Ok(allLogs);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Failed to retrieve audit logs", error = ex.Message });
        }
    }
}
