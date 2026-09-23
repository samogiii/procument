using Procument.Shared.Entities;

namespace Procument.Module.Identity.Entities;

/// <summary>A revocable, rotating browser session token. Only its SHA-256 hash is stored.</summary>
public sealed class RefreshToken : BaseEntity
{
    public long UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedByIp { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedByIp { get; set; }
    public string? ReplacedByTokenHash { get; set; }

    public User User { get; set; } = null!;
}
