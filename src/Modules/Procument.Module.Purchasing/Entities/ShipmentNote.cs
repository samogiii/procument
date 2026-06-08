using Procument.Module.Identity.Entities;
using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Entities;

public class ShipmentNote : BaseEntity
{
    /// <summary>Auto-generated: SN-{yyyy}-{seq:000}, e.g. SN-2026-001</summary>
    public string SNNumber { get; set; } = string.Empty;

    public long WarehouseId { get; set; }

    public string? TId { get; set; }
    public string? SONumber { get; set; }
    public string? Destination { get; set; }
    public string? AWBNumber { get; set; }

    /// <summary>Stored file name for the uploaded SN# PDF.</summary>
    public string? PdfFileName { get; set; }

    /// <summary>"CPT" | "DDP" — determines whether items must share one customer (CPT) or can be mixed (DDP).</summary>
    public string Type { get; set; } = "DDP";

    /// <summary>
    /// Draft → Waiting for Packing → Ship To USA → Clearing Customs → Received in Office → Delivered to Customer
    /// (CPT skips Received in Office and goes directly to Delivered to Customer after Clearing Customs)
    /// </summary>
    public string Status { get; set; } = "Draft";

    /// <summary>Stored file name for the customs document uploaded by the SYD/shipping team.</summary>
    public string? CustomsFileName { get; set; }
    public string? CustomsOriginalFileName { get; set; }
    public DateTime? CustomsUploadedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long CreatedByUserId { get; set; }

    // Navigation
    public Warehouse Warehouse { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
    public ICollection<ShipmentNoteTrackNumber> TrackNumbers { get; set; } = new List<ShipmentNoteTrackNumber>();
    public ICollection<ShipmentNoteBox> Boxes { get; set; } = new List<ShipmentNoteBox>();
}
