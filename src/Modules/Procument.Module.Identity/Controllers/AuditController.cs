using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procument.Module.Identity.Services;
using Procument.Shared.Entities;

namespace Procument.Module.Identity.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Expert")]
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
}
