using Procument.Module.Catalog.Entities;
using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Entities;

/// <summary>
/// Cloned snapshot of an InvoiceItem (and its RFQ / Quote / selected supplier chain) that
/// admins can edit without touching the source rows. Editable fields drive POItem creation
/// when the parent Procurement is finalized.
/// </summary>
public class ProcurementItem : BaseEntity
{
    // ──── RFQ snapshot (read-only, cloned at creation) ────
    public long? SourceRfqId { get; set; }
    public long? SourceRfqItemId { get; set; }
    public string? RfqName { get; set; }
    public int? RfqExType { get; set; }

    // PartNumber: lookup FK (parts are immutable reference data — no snapshot columns needed)
    public long? PartNumberId { get; set; }
    public string? PartNumberName { get; set; }
    public string? PartNumberDescription { get; set; }

    public double? RfqQty { get; set; }
    public string? RfqCondition { get; set; }
    public string? RfqUnit { get; set; }
    public string? RfqPriority { get; set; }
    public string? RfqAlt { get; set; }
    public string? RfqNote { get; set; }

    // ──── Quote snapshot ────
    public long? SourceQuoteId { get; set; }
    public long? SourceQuoteItemId { get; set; }
    public string? QuoteNumber { get; set; }
    public decimal QuoteUnitPrice { get; set; }
    public int QuoteQty { get; set; }
    public string? QuoteCondition { get; set; }
    public string? QuoteAlt { get; set; }
    public int? QuoteLeadTimeDays { get; set; }

    // ──── Selected-supplier snapshot (from ProcumentRecord at clone time) ────
    public long? SourceProcumentRecordId { get; set; }
    public long? SourceSupplierId { get; set; }
    public string? SupplierName { get; set; }
    public decimal? SupplierPrice { get; set; }
    public string? SupplierLeadTime { get; set; }
    public string? SupplierCondition { get; set; }
    public string? SupplierCertName { get; set; }
    public double? ShippingCost { get; set; }

    // ──── Invoice acceptance snapshot ────
    public long SourceInvoiceItemId { get; set; }
    public int AcceptedQty { get; set; }
    public decimal AcceptedUnitPrice { get; set; }

    // ──── Editable fields (admins mutate these) ────
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public long? CurrentSupplierId { get; set; }
    public string? LeadTime { get; set; }
    public string? Condition { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public string? Alt { get; set; }
    public string? Note { get; set; }

    /// <summary>Open | Sourcing | Ready | Cancelled | Returned (flipped when a PO was returned and this item is back in play).</summary>
    public string ItemStatus { get; set; } = "Open";

    // ──── Loop / Recycle tracking ────
    /// <summary>Incremented every time a PO containing this item is returned. Hard-capped at 5 to prevent runaway loops.</summary>
    public int LoopCount { get; set; }
    public string? LastReturnReason { get; set; }
    public DateTime? LastReturnedAt { get; set; }
    /// <summary>Stamped when the PO that consumed this item is marked Completed — terminal, closes the loop.</summary>
    public long? FulfilledByPOItemId { get; set; }

    // ──── Foreign keys ────
    public long ProcurementId { get; set; }

    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // ──── Navigation ────
    public Procurement Procurement { get; set; } = null!;
    public PartNumber? PartNumber { get; set; }
    public Supplier? CurrentSupplier { get; set; }
    public ICollection<ProcurementSupplierQuote> SupplierQuotes { get; set; } = new List<ProcurementSupplierQuote>();
}
