using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Entities;

public class POItemTrackNumber : BaseEntity
{
    public string TrackNumber { get; set; } = string.Empty;
    public string? Carrier { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Foreign key
    public long POItemId { get; set; }

    // Navigation
    public POItem POItem { get; set; } = null!;
}
