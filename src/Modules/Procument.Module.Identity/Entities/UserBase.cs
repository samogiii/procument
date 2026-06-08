using Procument.Shared.Entities;

namespace Procument.Module.Identity.Entities;

/// <summary>Maps a user to one or more bases (company/location groups).
/// Base values correspond to CompanyPreset.SortOrder (e.g. 1, 2, 4, 5).</summary>
public class UserBase : BaseEntity
{
    public long UserId { get; set; }
    /// <summary>Matches Customer.Base / CompanyPreset.SortOrder.</summary>
    public int Base { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
