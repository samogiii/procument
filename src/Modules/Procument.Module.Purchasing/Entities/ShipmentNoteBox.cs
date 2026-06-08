using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Entities;

/// <summary>
/// Physical box in a Shipment Note — entered by the inventory/warehouse team when packing.
/// One SN can have multiple boxes from different track numbers.
/// </summary>
public class ShipmentNoteBox : BaseEntity
{
    public long ShipmentNoteId { get; set; }
    public int BoxNumber { get; set; }
    public long? TrackNumberId { get; set; }  // which track this box came from (optional)
    public decimal? WeightKg { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? WidthCm { get; set; }
    public decimal? LengthCm { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ShipmentNote ShipmentNote { get; set; } = null!;
    public POItemTrackNumber? TrackNumber { get; set; }
}
