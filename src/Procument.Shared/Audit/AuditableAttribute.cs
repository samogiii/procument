namespace Procument.Shared.Audit;

/// <summary>
/// Marks a controller action for automatic audit logging.
/// The filter extracts user, HTTP method, route, and entity info automatically.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class AuditableAttribute : Attribute
{
    /// <summary>Entity type being acted on (e.g., "Quote", "RFQ", "User")</summary>
    public string EntityName { get; }

    /// <summary>Custom action name. If null, derived from HTTP method (GET→View, POST→Create, PUT→Update, PATCH→Patch, DELETE→Delete)</summary>
    public string? Action { get; }

    /// <summary>Route parameter name that contains the entity ID (default: "id")</summary>
    public string EntityIdParam { get; set; } = "id";

    /// <summary>If true, captures the request body as audit details</summary>
    public bool CaptureBody { get; set; } = false;

    public AuditableAttribute(string entityName, string? action = null)
    {
        EntityName = entityName;
        Action = action;
    }
}
