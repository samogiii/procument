using Procument.Module.Catalog.Entities;
using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Entities;

public class PurchaseOrder : BaseEntity
{
    public string PONumber { get; set; } = string.Empty;
    public decimal? TotalAmount { get; set; }
    public string Status { get; set; } = "Draft";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? RejectionNote { get; set; }

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

    // Foreign keys
    public long SupplierId { get; set; }
    public long? InvoiceId { get; set; }

    // Navigation
    public Supplier Supplier { get; set; } = null!;
    public ICollection<POItem> POItems { get; set; } = new List<POItem>();
    public POImportDetail? ImportDetail { get; set; }
}
