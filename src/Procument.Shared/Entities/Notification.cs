namespace Procument.Shared.Entities;

public class Notification : BaseEntity
{
    public long UserId { get; set; }
    public string Type { get; set; } = string.Empty; // "StatusChange", "Rejection", "PendingApproval"
    public string EntityName { get; set; } = string.Empty; // "Quote", "Invoice", "PurchaseOrder"
    public long EntityId { get; set; }
    public string EntityNumber { get; set; } = string.Empty; // e.g. "QT-001"
    public string Message { get; set; } = string.Empty;
    public string? RejectionNote { get; set; }
    public bool IsRead { get; set; } = false;
    public bool IsDismissed { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
