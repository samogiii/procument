using Procument.Shared.Entities;

namespace Procument.Module.Sales.Entities;

/// <summary>
/// How far one user has read the chat of one Customer Follow Ups row. A row is unread for the user
/// when someone else sent a message after LastReadMessageId, or the user marked it unread.
/// No record yet = nothing read.
/// </summary>
public class CustomerFollowUpMessageRead : BaseEntity
{
    public long CustomerFollowUpId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public long LastReadMessageId { get; set; }
    /// <summary>Set by "Mark as unread"; cleared the next time the user opens the chat.</summary>
    public bool MarkedUnread { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public CustomerFollowUp CustomerFollowUp { get; set; } = null!;
}
