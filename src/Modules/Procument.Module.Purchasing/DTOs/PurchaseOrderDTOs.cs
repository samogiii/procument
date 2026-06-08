namespace Procument.Module.Purchasing.DTOs;

// ──── Request DTOs ────

/// <summary>Create PO by assigning existing unassigned POItems to a new PO.</summary>
public class CreatePORequest
{
    public long SupplierId { get; set; }
    public long? InvoiceId { get; set; }
    public List<long> POItemIds { get; set; } = new();
    /// <summary>Wallet chosen at creation time — used as the default debit wallet on payment acceptance.</summary>
    public long? PreferredWalletId { get; set; }
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
    public DateTime? PODate { get; set; }
    public decimal? TotalAmount { get; set; }
    public string Status { get; set; } = "Draft";
    public DateTime CreatedAt { get; set; }
    public long SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public long? InvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public long? CustomerId { get; set; }
    public string? RejectionNote { get; set; }
    public string AdminApproval { get; set; } = "Pending";
    public string? AdminApprovalNote { get; set; }
    public DateTime? AdminApprovalAt { get; set; }
    public string PaymentStatus { get; set; } = "NotStarted";
    public DateTime? PaymentSubmittedAt { get; set; }
    public string PaymentApproval { get; set; } = "Pending";
    public string? PaymentApprovalNote { get; set; }
    public DateTime? PaymentApprovalAt { get; set; }
    // ─── PDF totals adjustments (editable on PO page; pre-populated in PDF generator) ───
    public decimal? ProcessingFee { get; set; }
    public decimal? Shipping { get; set; }
    public decimal? Tax { get; set; }
    /// <summary>Wallet chosen at creation time — pre-selected in the payment acceptance wallet picker.</summary>
    public long? PreferredWalletId { get; set; }
    public string? PreferredWalletName { get; set; }
    public string? PreferredWalletCompany { get; set; }
    public List<POItemResponse> Items { get; set; } = new();
    public int AcceptedTrackItems { get; set; }
    public int TotalTrackItems { get; set; }
}

/// <summary>Update PO-level cost adjustments (Processing Fee, Shipping, Tax) shown on the PDF.</summary>
public class UpdatePOTotalsRequest
{
    public DateTime? PODate { get; set; }
    public decimal? ProcessingFee { get; set; }
    public decimal? Shipping { get; set; }
    public decimal? Tax { get; set; }
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

public class PopWithdrawRequest
{
    /// <summary>The wallet (PaymentBox) to debit when the POP is uploaded.</summary>
    public long WalletId { get; set; }
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
    public string? SwiftCode { get; set; }
    public string? ABA { get; set; }
    public decimal? Wirefee { get; set; }
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
    public string? SwiftCode { get; set; }
    public string? ABA { get; set; }
    public decimal? Wirefee { get; set; }
}

// ──── Track Number DTOs ────

public class TrackNumberResponse
{
    public long Id { get; set; }
    public long POItemId { get; set; }
    public string TrackNumber { get; set; } = string.Empty;
    public string? Carrier { get; set; }
    public string? Notes { get; set; }
    public long? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime CreatedAt { get; set; }
}

public class SaveTrackNumberRequest
{
    public string TrackNumber { get; set; } = string.Empty;
    public string? Carrier { get; set; }
    public string? Notes { get; set; }
    public long? WarehouseId { get; set; }
}

/// <summary>Admin summary of all track numbers across all POs, with their items.</summary>
public class TrackNumberSummaryResponse
{
    public long Id { get; set; }
    public string TrackNumber { get; set; } = string.Empty;
    public string? Carrier { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "Active";
    public long? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public string? WarehouseAddress { get; set; }
    public long POItemId { get; set; }
    public long? POId { get; set; }
    public string? PONumber { get; set; }
    public string? PartNumberName { get; set; }
    public string? Description { get; set; }
    public string? SupplierName { get; set; }
    public int Qty { get; set; }
    public string? Condition { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerCode { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<TrackSummaryItem> Items { get; set; } = new();
    public List<TrackSummaryDocument> Documents { get; set; } = new();
    public List<TrackSummaryBox> ReceivedBoxes { get; set; } = new();
    public List<TrackSummarySnBox> SnBoxes { get; set; } = new();
}

public class TrackSummaryItem
{
    public long Id { get; set; }
    public long POItemId { get; set; }
    public string? PartNumberName { get; set; }
    public string? Description { get; set; }
    public string? SupplierName { get; set; }
    public int Qty { get; set; }
    public string? Condition { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerCode { get; set; }
    public int ExpectedQty { get; set; }
    public int? ActualQty { get; set; }
    public bool? IsAvailable { get; set; }
    public string Status { get; set; } = "Pending";
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
}

public class TrackSummaryDocument
{
    public long Id { get; set; }
    public long? POItemId { get; set; }
    public string? PartNumberName { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string? MimeType { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; }
    public string? UploadedByName { get; set; }
}

public class TrackSummaryBox
{
    public long Id { get; set; }
    public int BoxNumber { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? WidthCm { get; set; }
    public decimal? LengthCm { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TrackSummarySnBox
{
    public long Id { get; set; }
    public long ShipmentNoteId { get; set; }
    public string? SNNumber { get; set; }
    public int BoxNumber { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? WidthCm { get; set; }
    public decimal? LengthCm { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ──── Total P/N (TPP) view DTOs ────

/// <summary>One row of the Total P/N (TPP) report. Mirrors the columns of totalpn.xlsx.
/// One row per POItem, joined across PO → Procurement → Invoice → Quote → FinalInvoice → Customer.</summary>
public class TotalPNRowResponse
{
    public long Id { get; set; }                       // POItem.Id (used by inline edit)
    public long? PurchaseOrderId { get; set; }         // PurchaseOrder.Id (used for navigation link)
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
    public string? SerialNumber { get; set; }          // SN# — from ShipmentNote(s) linked to this PO item's track numbers
    public string? ShippingStatus { get; set; }        // POItemTrackNumber.Status (most recent / joined)
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
    // ── Shipment Note fields (populated only in Total Order view) ──
    public string? TId { get; set; }                   // ShipmentNote.TId
    public string? SONumber { get; set; }              // ShipmentNote.SONumber
    public string? AwbNumber { get; set; }             // ShipmentNote.AWBNumber
}

/// <summary>Inline edit for the two new POItem fields exposed by the Total P/N grid.</summary>
public class UpdatePOItemTotalPNRequest
{
    public string? Status { get; set; }
    public string? Note { get; set; }
}

/// <summary>SuperAdmin-only price override for a POItem. Recalculates TotalPrice and PO.TotalAmount.</summary>
public class UpdatePOItemPriceRequest
{
    public decimal UnitPrice { get; set; }
}

/// <summary>Unique values per filterable column — used to populate the filter dropdowns client-side.</summary>
public class TotalPNFilterOptions
{
    public List<string> Customers { get; set; } = [];
    public List<string> InvoiceNumbers { get; set; } = [];
    public List<string> PartNumbers { get; set; } = [];
    public List<string> Conditions { get; set; } = [];
    public List<string> PoNumbers { get; set; } = [];
    public List<string> Suppliers { get; set; } = [];
    public List<string> PaymentTerms { get; set; } = [];
    public List<string> Statuses { get; set; } = [];
    public List<string> ShippingStatuses { get; set; } = [];
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
