using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Entities;

public class ShipmentNoteTrackNumber : BaseEntity
{
    public long ShipmentNoteId { get; set; }
    public long TrackNumberId { get; set; }

    // Navigation
    public ShipmentNote ShipmentNote { get; set; } = null!;
    public POItemTrackNumber TrackNumber { get; set; } = null!;
}
