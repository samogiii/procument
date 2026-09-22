using Procument.Module.Catalog.Entities;
using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Entities;

public class PurchaseOrder : BaseEntity
{
    /// <summary>Customer for the existing sales-driven flow; Stock for purchases made for our inventory.</summary>
    public string Origin { get; set; } = "Customer";
    public string PONumber { get; set; } = string.Empty;
    public DateTime? PODate { get; set; }
    public decimal? TotalAmount { get; set; }
    /// <summary>
    /// Draft | Waiting For Admin Approval | Waiting For Payment | Payment Done | Ship To Warehouse 1..3 |
    /// Ship To Customer | Completed | Cancelled | Returned (items recycled back into Procurement).
    /// </summary>
    public string Status { get; set; } = "Draft";
    /// <summary>Optional fulfilment route selected while the PO is in progress: EndUser or In Shop.</summary>
    public string? FulfillmentMode { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? RejectionNote { get; set; }
    /// <summary>Free-text subject/title for the PO (mirrors Invoice.Subject). Editable on the PO page.</summary>
    public string? Subject { get; set; }

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

    

    // ─── Payment Wallet Preference ───
    /// <summary>Wallet selected at PO creation time — used as the default debit wallet on payment acceptance.</summary>
    public long? PreferredWalletId { get; set; }
    /// <summary>The buying company, independent of the wallet used for each payment.</summary>
    public long? CompanyPresetId { get; set; }
    public long? DestinationWarehouseId { get; set; }
    public string? SupplierPIRef { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    // Foreign keys
    public long SupplierId { get; set; }
    public long? InvoiceId { get; set; }

    // Navigation
    public Supplier Supplier { get; set; } = null!;
    public Warehouse? DestinationWarehouse { get; set; }
    public ICollection<POItem> POItems { get; set; } = new List<POItem>();
    public ICollection<PurchaseOrderDocument> Documents { get; set; } = new List<PurchaseOrderDocument>();
    public POImportDetail? ImportDetail { get; set; }
}
