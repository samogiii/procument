using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Entities;

/// <summary>
/// Batch header for the post-acceptance, pre-PO editing layer.
/// One Procurement is created per accepted proforma invoice. It holds cloned snapshots
/// of the RFQ / Quote / supplier-quote data so admins can edit qty / unit price / supplier
/// without bleeding back into RFQItem, QuoteItem, or ProcumentRecord.
/// Note: no navigation to Invoice (lives in Sales module, which references Purchasing).
/// The FK relationship is configured in AppDbContext as a shadow navigation.
/// </summary>
public class Procurement : BaseEntity
{
    public string ProcurementNumber { get; set; } = string.Empty;

    /// <summary>Open | InProgress | Finalized | Cancelled</summary>
    public string Status { get; set; } = "Open";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long? CreatedByUserId { get; set; }
    public DateTime? FinalizedAt { get; set; }
    public long? FinalizedByUserId { get; set; }
    public string? Notes { get; set; }

    // Foreign keys (shadow nav to Invoice — configured in AppDbContext)
    public long InvoiceId { get; set; }

    // Navigation
    public ICollection<ProcurementItem> Items { get; set; } = new List<ProcurementItem>();
}
