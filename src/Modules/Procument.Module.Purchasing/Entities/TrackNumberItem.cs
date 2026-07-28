using Procument.Module.Identity.Entities;
using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Entities;

public class TrackNumberItem : BaseEntity
{
    public long TrackNumberId { get; set; }   // FK → POItemTrackNumber.Id
    public long POItemId { get; set; }         // FK → POItem.Id

    public int ExpectedQty { get; set; }
    public int? ActualQty { get; set; }
    public bool? IsAvailable { get; set; }

    /// <summary>
    /// Units shipped out of this warehouse on a <see cref="WarehouseTransfer"/>.
    /// Remaining stock here is <c>ActualQty - TransferredOutQty</c> — Ready-for-SN must use that,
    /// otherwise the same units count at both the source and destination warehouse.
    /// </summary>
    public int TransferredOutQty { get; set; }

    /// <summary>"Pending" | "Accepted" | "Rejected"</summary>
    public string Status { get; set; } = "Pending";

    public bool? CertNeeded { get; set; }

    public long? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public POItemTrackNumber TrackNumber { get; set; } = null!;
    public POItem POItem { get; set; } = null!;
    public User? ReviewedBy { get; set; }
}
