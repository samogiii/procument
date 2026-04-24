using Procument.Module.Catalog.Entities;
using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Entities;

public class POItem : BaseEntity
{
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string? Condition { get; set; }
    public long? SupplierId { get; set; }

    // Foreign keys
    public long? POId { get; set; }
    public long? ProcumentId { get; set; }
    public long? PartNumberId { get; set; }
    public long? InvoiceItemId { get; set; }
    /// <summary>Trace back to the Procurement snapshot this POItem was materialized from (nullable for legacy rows).</summary>
    public long? SourceProcurementItemId { get; set; }

    // ─── Return / Recycle tracking (soft-delete when a POItem is recycled back into Procurement) ───
    /// <summary>Soft-delete marker. Set when this item was returned to Procurement. Unassigned-item queries filter these out.</summary>
    public DateTime? ReturnedAt { get; set; }
    /// <summary>Audit pointer: the PO this item was attached to at the moment it was returned.</summary>
    public long? ReturnedFromPOId { get; set; }
    public string? ReturnReason { get; set; }

    // Navigation
    public PurchaseOrder? PurchaseOrder { get; set; }
    public ProcumentRecord? ProcumentRecord { get; set; }
    public PartNumber? PartNumber { get; set; }
    public ProcurementItem? SourceProcurementItem { get; set; }
    public ICollection<POItemTrackNumber> TrackNumbers { get; set; } = new List<POItemTrackNumber>();
}
