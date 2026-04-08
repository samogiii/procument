using System;

namespace Procument.Shared.Entities;

public class AuditLog : BaseEntity
{
    public long? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Details { get; set; }
    public string? IPAddress { get; set; }

    // New fields for detailed tracking
    public string? UserName { get; set; }
    public string? OldValues { get; set; }      // JSON
    public string? NewValues { get; set; }      // JSON
    public string? AffectedColumns { get; set; } // JSON array or comma-separated
    public string? ContextData { get; set; }    // JSON for additional context

    // Business audit fields for human-readable display
    public string? ActionCategory { get; set; }  // Creation, StatusChange, Update, ItemChange, Deletion
    public string? EntityDisplayName { get; set; }  // e.g., "RFQ #123" instead of just "RFQ"
    public string? RelatedEntityId { get; set; }  // Related entity ID (e.g., Quote ID for an RFQ status change)
    public string? RelatedEntityType { get; set; }  // Related entity type
}
