using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Entities;

public class POItemTrackNumber : BaseEntity
{
    public string TrackNumber { get; set; } = string.Empty;
    public string? Carrier { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Warehouse this shipment is destined for
    public long? WarehouseId { get; set; }

    public string Status { get; set; } = "Ship to Warehouse";

    // ─── Warehouse-to-warehouse transfer chain ───
    /// <summary>"Supplier" (arrived from a supplier) or "Transfer" (emitted by a WarehouseTransfer).</summary>
    public string Origin { get; set; } = "Supplier";

    /// <summary>Set when this leg was emitted by a warehouse transfer. Null for supplier shipments.</summary>
    public long? SourceTransferId { get; set; }

    /// <summary>The leg this stock arrived on before being transferred. Walk this to rebuild the trace.</summary>
    public long? ParentTrackNumberId { get; set; }

    // Foreign key
    public long POItemId { get; set; }

    // Navigation
    public POItem POItem { get; set; } = null!;
    public Warehouse? Warehouse { get; set; }
    public WarehouseTransfer? SourceTransfer { get; set; }
    public POItemTrackNumber? ParentTrackNumber { get; set; }
    public ICollection<TrackNumberItem> Items { get; set; } = new List<TrackNumberItem>();
    public ICollection<TrackNumberDocument> Documents { get; set; } = new List<TrackNumberDocument>();
    public ICollection<ShipmentNoteTrackNumber> ShipmentNotes { get; set; } = new List<ShipmentNoteTrackNumber>();
    public ICollection<TrackNumberBox> Boxes { get; set; } = new List<TrackNumberBox>();

    // ── Shipping status values ──────────────────────────────────────────────
    // "Ship to Warehouse"        – set when track number is created
    // "Received in Warehouse"    – set when inventory user submits/verifies items
    // "Waiting for Packing"      – set when admin creates SN# with this track (end state for Inventory)
    // "Ship To USA"              – set when SN boxes + AWB are finalized
    // "Clearing Customs"         – set when customs file is uploaded
    // "Received in Office"       – admin manual (DDP only)
    // "Delivered to Customer"    – admin manual or auto (CPT after customs upload)
}
