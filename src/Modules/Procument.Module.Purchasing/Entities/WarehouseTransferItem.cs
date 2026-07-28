using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Entities;

/// <summary>
/// One part line on a <see cref="WarehouseTransfer"/>. Quantities are per-item, so a line may move
/// only part of what is sitting at the source warehouse.
/// </summary>
public class WarehouseTransferItem : BaseEntity
{
    public long WarehouseTransferId { get; set; }

    /// <summary>The verified receipt line at the SOURCE warehouse that this stock is drawn from.</summary>
    public long SourceTrackNumberItemId { get; set; }

    /// <summary>Denormalized from the source line — the part being moved.</summary>
    public long POItemId { get; set; }

    /// <summary>Quantity leaving the source warehouse.</summary>
    public int Qty { get; set; }

    /// <summary>Quantity confirmed at the destination warehouse. Null until received.</summary>
    public int? ReceivedQty { get; set; }

    /// <summary>"In Transit" | "Received" | "Short" | "Cancelled"</summary>
    public string Status { get; set; } = "In Transit";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public WarehouseTransfer Transfer { get; set; } = null!;
    public TrackNumberItem SourceTrackNumberItem { get; set; } = null!;
    public POItem POItem { get; set; } = null!;
}
