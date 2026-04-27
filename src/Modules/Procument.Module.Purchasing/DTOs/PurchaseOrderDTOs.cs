namespace Procument.Module.Purchasing.DTOs;

// ──── Request DTOs ────

/// <summary>Create PO by assigning existing unassigned POItems to a new PO.</summary>
public class CreatePORequest
{
    public long SupplierId { get; set; }
    public long? InvoiceId { get; set; }
    public List<long> POItemIds { get; set; } = new();
}

/// <summary>Update a single POItem (supplier, qty, unitPrice).</summary>
public class UpdatePOItemRequest
{
    public long Id { get; set; }
    public long? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
}

public class UpdatePOStatusRequest
{
    public string Status { get; set; } = string.Empty;
    public string? RejectionNote { get; set; }
}

// ──── Response DTOs ────

public class POResponse
{
    public long Id { get; set; }
    public string PONumber { get; set; } = string.Empty;
    public decimal? TotalAmount { get; set; }
    public string Status { get; set; } = "Draft";
    public DateTime CreatedAt { get; set; }
    public long SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public long? InvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? RejectionNote { get; set; }
    public string AdminApproval { get; set; } = "Pending";
    public string? AdminApprovalNote { get; set; }
    public DateTime? AdminApprovalAt { get; set; }
    public string PaymentStatus { get; set; } = "NotStarted";
    public DateTime? PaymentSubmittedAt { get; set; }
    public string PaymentApproval { get; set; } = "Pending";
    public string? PaymentApprovalNote { get; set; }
    public DateTime? PaymentApprovalAt { get; set; }
    public List<POItemResponse> Items { get; set; } = new();
}

public class UpdateAdminApprovalRequest
{
    /// <summary>Approved | Rejected</summary>
    public string Decision { get; set; } = string.Empty;
    public string? Note { get; set; }
}

public class UpdatePaymentApprovalRequest
{
    /// <summary>Accepted | Rejected</summary>
    public string Decision { get; set; } = string.Empty;
    public string? Note { get; set; }
}

public class POItemResponse
{
    public long Id { get; set; }
    public long? POId { get; set; }
    public long? ProcumentId { get; set; }
    public long? PartNumberId { get; set; }
    public string? PartNumberName { get; set; }
    public string? Description { get; set; }
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string? Condition { get; set; }
    public long? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public List<TrackNumberResponse> TrackNumbers { get; set; } = new();
}

// ──── Import Detail DTOs ────

public class POImportDetailResponse
{
    public long Id { get; set; }
    public long PurchaseOrderId { get; set; }
    public string? BankName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankAddress { get; set; }
    public string? BankCity { get; set; }
    public string? BankCountry { get; set; }
    public string? FedExAccount { get; set; }
    public string? CourierName { get; set; }
    public string? ShippingMethod { get; set; }
    public string? Incoterms { get; set; }
    public string? Notes { get; set; }
}

public class SavePOImportDetailRequest
{
    public string? BankName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankAddress { get; set; }
    public string? BankCity { get; set; }
    public string? BankCountry { get; set; }
    public string? FedExAccount { get; set; }
    public string? CourierName { get; set; }
    public string? ShippingMethod { get; set; }
    public string? Incoterms { get; set; }
    public string? Notes { get; set; }
}

// ──── Track Number DTOs ────

public class TrackNumberResponse
{
    public long Id { get; set; }
    public long POItemId { get; set; }
    public string TrackNumber { get; set; } = string.Empty;
    public string? Carrier { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SaveTrackNumberRequest
{
    public string TrackNumber { get; set; } = string.Empty;
    public string? Carrier { get; set; }
    public string? Notes { get; set; }
}

// ──── Total P/N (TPP) view DTOs ────

/// <summary>One row of the Total P/N (TPP) report. Mirrors the columns of totalpn.xlsx.
/// One row per POItem, joined across PO → Procurement → Invoice → Quote → FinalInvoice → Customer.</summary>
public class TotalPNRowResponse
{
    public long Id { get; set; }                       // POItem.Id (used by inline edit)
    public string? PONumber { get; set; }              // PurchaseOrder.PONumber, null until assigned
    public int? PORef { get; set; }                    // POItem.PORef (line # within PO)
    public string? QuotationExpert { get; set; }       // Quote.User.Name
    public string? ProcurementExpert { get; set; }     // Procurement.CreatedByUser.Name
    public string? Customer { get; set; }              // Invoice.Customer.Name
    public string? Supplier { get; set; }              // resolved supplier name
    public string? PartNumber { get; set; }            // POItem.PartNumber.Name
    public string? Description { get; set; }           // POItem.PartNumber.Description
    public int Qty { get; set; }                       // POItem.Qty
    public string? Condition { get; set; }             // POItem.Condition
    public string? Priority { get; set; }              // ProcurementItem.RfqPriority
    public string? Warehouse { get; set; }             // RFQ.ExType → "Warehouse" / "Vendor" / "Customer"
    public string? SerialNumber { get; set; }          // SN# — null for now
    public string? CustomerInvoiceNumber { get; set; } // Invoice.InvoiceNumber (PI# to Customer)
    public decimal PurchasingUnitPriceUsd { get; set; }
    public decimal PurchasingTotalPriceUsd { get; set; }
    public decimal? POAmount { get; set; }             // PurchaseOrder.TotalAmount
    public string? DPNumber { get; set; }              // DP# — null for now
    public string? SupplierDeliveryTime { get; set; }  // ProcurementItem.LeadTime
    public string? Status { get; set; }                // POItem.Status (NEW)
    public decimal SellingUnitPriceUsd { get; set; }   // QuoteItem.UnitPrice
    public decimal SellingTotalPriceUsd { get; set; }  // qty × QuoteItem.UnitPrice
    public decimal SellingUnitPriceYuan { get; set; }  // sellingUSD × Rate
    public decimal SellingTotalPriceYuan { get; set; }
    public decimal? InvAmount { get; set; }            // FinalInvoice.TotalAmount
    public DateTime? PODate { get; set; }              // PurchaseOrder.CreatedAt
    public DateTime? InvDate { get; set; }             // FinalInvoice.CreatedAt
    public decimal? Received { get; set; }             // sum of CustomerPayment.Amount on the proforma
    public DateTime? ReceivedDate { get; set; }        // most-recent CustomerPayment.CreatedAt
    public string? PaymentTerm { get; set; }           // Invoice.Status (Net30/Paid/Accepted)
    public DateTime? CustomerDeliveryTime { get; set; }// Invoice.DueDate
    public decimal Rate { get; set; }                  // Customer.CurrencyType=="Yuan"|"Both" → 7, else 1
    public string? TrackNumbers { get; set; }          // joined POItemTrackNumber.TrackNumber list
    public decimal? ShippingCost { get; set; }         // ProcurementItem.ShippingCost
    public string? Note { get; set; }                  // POItem.Note (NEW)
}

/// <summary>Inline edit for the two new POItem fields exposed by the Total P/N grid.</summary>
public class UpdatePOItemTotalPNRequest
{
    public string? Status { get; set; }
    public string? Note { get; set; }
}

/// <summary>Unassigned POItem response — enriched with ExType, customer, supplier info.</summary>
public class UnassignedPOItemResponse
{
    public long Id { get; set; }
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string? Condition { get; set; }
    public long? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public long? PartNumberId { get; set; }
    public string? PartNumberName { get; set; }
    public string? Alt { get; set; }
    public long? ProcumentId { get; set; }
    public long? InvoiceItemId { get; set; }
    public int? ExType { get; set; }
    public string? CustomerName { get; set; }
    public long? InvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
}
