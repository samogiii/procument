namespace Procument.Shared.Entities;

public class Notification : BaseEntity
{
    public long UserId { get; set; }
    public string Type { get; set; } = string.Empty; // "StatusChange","Rejection","PendingApproval","TrackAdded","TrackSubmitted","PartAccepted","PartRejected","TrackRejected","ReadyForSN"
    public string EntityName { get; set; } = string.Empty; // "Quote","Invoice","PurchaseOrder","TrackNumber"
    public long EntityId { get; set; }
    public string EntityNumber { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? RejectionNote { get; set; }
    public long? TriggeredByUserId { get; set; }
    public string? TriggeredByUserName { get; set; }
    public bool IsRead { get; set; } = false;
    public bool IsDismissed { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
