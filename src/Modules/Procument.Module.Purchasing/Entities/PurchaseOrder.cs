using Procument.Module.Catalog.Entities;
using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Entities;

public class PurchaseOrder : BaseEntity
{
    public string PONumber { get; set; } = string.Empty;
    public decimal? TotalAmount { get; set; }
    /// <summary>
    /// Draft | Waiting For Admin Approval | Waiting For Payment | Payment Done | Ship To Warehouse 1..3 |
    /// Ship To Customer | Completed | Cancelled | Returned (items recycled back into Procurement).
    /// </summary>
    public string Status { get; set; } = "Draft";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? RejectionNote { get; set; }

    // ─── Return / Recycle Workflow (loop back into Procurement) ───
    /// <summary>Reason captured when the PO (or some items) were returned to Procurement.</summary>
    public string? ReturnReason { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public long? ReturnedByUserId { get; set; }

    // ─── Admin Approval Workflow ───
    /// <summary>Pending | Approved | Rejected. Controls visibility to Payment role.</summary>
    public string AdminApproval { get; set; } = "Pending";
    public string? AdminApprovalNote { get; set; }
    public DateTime? AdminApprovalAt { get; set; }
    public long? AdminApprovalBy { get; set; }

    // ─── Payment Workflow ───
    /// <summary>NotStarted | Submitted.</summary>
    public string PaymentStatus { get; set; } = "NotStarted";
    public DateTime? PaymentSubmittedAt { get; set; }
    public long? PaymentSubmittedBy { get; set; }

    // ─── Payment Approval Workflow ───
    /// <summary>Pending | Accepted | Rejected.</summary>
    public string PaymentApproval { get; set; } = "Pending";
    public string? PaymentApprovalNote { get; set; }
    public DateTime? PaymentApprovalAt { get; set; }
    public long? PaymentApprovalBy { get; set; }

    // ─── Cost adjustments (rendered in the PO PDF totals block) ───
    /// <summary>Flat processing-fee amount shown on the PO PDF totals.</summary>
    public decimal? ProcessingFee { get; set; }
    /// <summary>PO-level shipping amount (independent of per-item ProcumentRecord.ShippingCost).</summary>
    public decimal? Shipping { get; set; }
    /// <summary>Flat tax amount shown on the PO PDF totals.</summary>
    public decimal? Tax { get; set; }

    // Foreign keys
    public long SupplierId { get; set; }
    public long? InvoiceId { get; set; }

    // Navigation
    public Supplier Supplier { get; set; } = null!;
    public ICollection<POItem> POItems { get; set; } = new List<POItem>();
    public POImportDetail? ImportDetail { get; set; }
}
