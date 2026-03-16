namespace Procument.Shared.Entities;

public class RFQUserRead : BaseEntity
{
    public long RFQId { get; set; }
    public long UserId { get; set; }
    public bool IsRead { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
