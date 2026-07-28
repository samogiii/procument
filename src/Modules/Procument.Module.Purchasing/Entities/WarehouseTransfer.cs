using Procument.Module.Identity.Entities;
using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Entities;

/// <summary>
/// One physical movement of stock between two warehouses, carried under its own track number.
/// Creating a transfer emits a destination <see cref="POItemTrackNumber"/> leg, so the receiving
/// warehouse verifies the goods through the normal Shipping flow.
/// </summary>
public class WarehouseTransfer : BaseEntity
{
    /// <summary>Auto-generated: WT-{yyyy}-{seq:000}, e.g. WT-2026-001</summary>
    public string TransferNumber { get; set; } = string.Empty;

    public long FromWarehouseId { get; set; }
    public long ToWarehouseId { get; set; }

    /// <summary>Carrier tracking number for this leg. Copied onto the destination track rows.</summary>
    public string TrackNumber { get; set; } = string.Empty;
    public string? Carrier { get; set; }
    public string? Notes { get; set; }

    /// <summary>"In Transit" | "Partially Received" | "Received" | "Cancelled"</summary>
    public string Status { get; set; } = "In Transit";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long CreatedByUserId { get; set; }

    public DateTime? ReceivedAt { get; set; }
    public long? ReceivedByUserId { get; set; }

    // Navigation
    public Warehouse FromWarehouse { get; set; } = null!;
    public Warehouse ToWarehouse { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
    public User? ReceivedBy { get; set; }
    public ICollection<WarehouseTransferItem> Items { get; set; } = new List<WarehouseTransferItem>();

    /// <summary>Destination legs emitted by this transfer (one per distinct POItem).</summary>
    public ICollection<POItemTrackNumber> DestinationTracks { get; set; } = new List<POItemTrackNumber>();
}
