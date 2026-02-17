using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Procument.Shared.Audit;
using Procument.Shared.Entities;

namespace Procument.API.Filters;

/// <summary>
/// Global action filter that intercepts controller actions decorated with [Auditable]
/// and automatically writes audit log entries.
/// </summary>
public class AuditActionFilter : IAsyncActionFilter
{
    private readonly DbContext _db;

    public AuditActionFilter(DbContext db)
    {
        _db = db;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Check if the action (or controller) has [Auditable]
        var auditAttr = context.ActionDescriptor.EndpointMetadata
            .OfType<AuditableAttribute>()
            .FirstOrDefault();

        if (auditAttr == null)
        {
            // No audit attribute — just run the action
            await next();
            return;
        }

        // Capture pre-execution info
        var httpMethod = context.HttpContext.Request.Method;
        var path = context.HttpContext.Request.Path.Value ?? "";
        var ipAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString();

        // Extract user ID from JWT claims
        long? userId = null;
        var idClaim = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)
                    ?? context.HttpContext.User.FindFirst("sub");
        if (idClaim != null && long.TryParse(idClaim.Value, out var uid))
        {
            userId = uid;
        }

        // Determine action name
        var action = auditAttr.Action ?? httpMethod switch
        {
            "GET" => "View",
            "POST" => "Create",
            "PUT" => "Update",
            "PATCH" => "Patch",
            "DELETE" => "Delete",
            _ => httpMethod
        };

        // Extract entity ID from route parameters
        var entityId = "";
        if (context.ActionArguments.TryGetValue(auditAttr.EntityIdParam, out var idVal) && idVal != null)
        {
            entityId = idVal.ToString() ?? "";
        }
        else if (context.RouteData.Values.TryGetValue(auditAttr.EntityIdParam, out var routeId) && routeId != null)
        {
            entityId = routeId.ToString() ?? "";
        }

        // Optionally capture request body summary
        string? details = null;
        if (auditAttr.CaptureBody)
        {
            // Grab the first [FromBody] argument
            var bodyArg = context.ActionArguments
                .FirstOrDefault(a => a.Value != null && a.Key != auditAttr.EntityIdParam);
            if (bodyArg.Value != null)
            {
                try
                {
                    details = JsonSerializer.Serialize(bodyArg.Value, new JsonSerializerOptions
                    {
                        WriteIndented = false,
                        MaxDepth = 3
                    });
                    // Truncate if too long
                    if (details.Length > 500)
                        details = details[..500] + "…";
                }
                catch
                {
                    details = bodyArg.Value.GetType().Name;
                }
            }
        }

        // Execute the actual action
        var executedContext = await next();

        // Only log if the action succeeded (2xx or 3xx)
        var statusCode = 0;
        if (executedContext.Result is ObjectResult objResult)
            statusCode = objResult.StatusCode ?? 200;
        else if (executedContext.Result is StatusCodeResult scResult)
            statusCode = scResult.StatusCode;
        else
            statusCode = 200; // default

        if (statusCode >= 200 && statusCode < 400)
        {
            // For POST responses that return the created entity, try to grab the ID
            if (string.IsNullOrEmpty(entityId) && executedContext.Result is ObjectResult createdResult)
            {
                if (createdResult.Value != null)
                {
                    // Try to get Id property from response
                    var idProp = createdResult.Value.GetType().GetProperty("Id")
                              ?? createdResult.Value.GetType().GetProperty("id");
                    if (idProp != null)
                    {
                        entityId = idProp.GetValue(createdResult.Value)?.ToString() ?? "";
                    }
                }
            }

            // Build details if not captured from body
            if (details == null)
            {
                details = $"{httpMethod} {path}";
            }

            var auditLog = new AuditLog
            {
                UserId = userId,
                Action = action,
                EntityName = auditAttr.EntityName,
                EntityId = entityId,
                Timestamp = DateTime.UtcNow,
                Details = details,
                IPAddress = ipAddress
            };

            _db.Set<AuditLog>().Add(auditLog);
            await _db.SaveChangesAsync();
        }
    }
}
