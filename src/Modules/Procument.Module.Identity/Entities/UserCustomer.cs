using Procument.Shared.Entities;

namespace Procument.Module.Identity.Entities;

/// <summary>Individually assigns a customer to a user regardless of base.
/// Allows a user to see and create RFQs for specific out-of-base customers.</summary>
public class UserCustomer : BaseEntity
{
    public long UserId { get; set; }
    public long CustomerId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
