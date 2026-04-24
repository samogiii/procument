using Procument.Module.Catalog.Entities;
using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Entities;

/// <summary>
/// Supplier offer row attached to a ProcurementItem. Mirrors the ProcumentRecord-backed
/// proc-grid on the RFQ page, but lives in its own table so edits don't leak back into
/// the source ProcumentRecord rows. One row can be IsSelected=true per ProcurementItem.
/// </summary>
public class ProcurementSupplierQuote : BaseEntity
{
    public long ProcurementItemId { get; set; }

    public long? SupplierId { get; set; }
    /// <summary>Denormalized supplier name — kept even if the Supplier row is later deleted.</summary>
    public string SupplierName { get; set; } = string.Empty;

    public decimal Price { get; set; }
    public double Qty { get; set; }
    public string? Condition { get; set; }
    public string? Unit { get; set; }
    public string? Alt { get; set; }
    public string? LeadTime { get; set; }
    public string? CertName { get; set; }
    public double? ShippingCost { get; set; }
    public string? Note { get; set; }
    public DateOnly? TagDate { get; set; }
    public string? ShippingPoint { get; set; }

    public bool IsSelected { get; set; }

    /// <summary>Audit pointer to the ProcumentRecord this was cloned from, if any.</summary>
    public long? SourceProcumentRecordId { get; set; }

    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long? AddedByUserId { get; set; }

    // Navigation
    public ProcurementItem ProcurementItem { get; set; } = null!;
    public Supplier? Supplier { get; set; }
}
