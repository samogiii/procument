using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Entities;

/// <summary>
/// Physical box received at the warehouse with a track number.
/// Inventory users enter these when the shipment arrives.
/// One TrackNumber can have multiple boxes.
/// </summary>
public class TrackNumberBox : BaseEntity
{
    public long TrackNumberId { get; set; }
    public int BoxNumber { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? WidthCm { get; set; }
    public decimal? LengthCm { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public POItemTrackNumber TrackNumber { get; set; } = null!;
}
