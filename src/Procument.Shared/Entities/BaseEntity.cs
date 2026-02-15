namespace Procument.Shared.Entities;

public abstract class BaseEntity
{
    public long Id { get; set; }
}

public abstract class AuditableEntity : BaseEntity
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ModifyAt { get; set; }
    public bool IsActive { get; set; } = true;
}
