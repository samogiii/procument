using Procument.Module.Sales.Entities;

namespace Procument.Module.Sales.DTOs;

public class CreateInvoiceRequest
{
    public long QuoteId { get; set; }
    public DateTime? DueDate { get; set; }
    public string? CustomerPONumber { get; set; }
    public string? Subject { get; set; }
    public string? PaymentStatus { get; set; }      // Net30 | CAD | Prepayment
    public decimal? PrepaymentPercent { get; set; } // 1-100, only when PaymentStatus = "Prepayment"
    public List<CreateInvoiceItemRequest> Items { get; set; } = new();
}

public class CreateInvoiceItemRequest
{
    public long QuoteItemId { get; set; }
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
}

public class UpdateInvoiceStatusRequest
{
    public string Status { get; set; } = string.Empty;
    public bool AutoFinalize { get; set; }
}

public class UpdateInvoiceRequest
{
    public DateTime? DueDate { get; set; }
    public string? CustomerPONumber { get; set; }
    public string? Subject { get; set; }
    public string? PaymentStatus { get; set; }
    public decimal? PrepaymentPercent { get; set; }
}

public class UpdateInvoiceItemDiscountRequest
{
    public long Id { get; set; }
    public decimal? FinalPrice { get; set; }  // user sets this; Discount = TotalPrice - FinalPrice
    // New: direct qty / unit-price edits. When provided, TotalPrice = Qty * UnitPrice,
    // and Discount is computed from QuoteItem.UnitPrice (the original quote price).
    public int? Qty { get; set; }
    public decimal? UnitPrice { get; set; }
}

public class UpdateInvoiceItemsRequest
{
    public List<UpdateInvoiceItemDiscountRequest> Items { get; set; } = new();
}

public class InvoiceResponse
{
    public long Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? PaymentStatus { get; set; }
    public decimal? PrepaymentPercent { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? PaidDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CustomerPONumber { get; set; }
    public string? Subject { get; set; }

    public long QuoteId { get; set; }
    public long CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerCode { get; set; }
    public string? CustomerContactPerson { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerBillTo { get; set; }
    public string? CustomerShipTo { get; set; }
    public string? CustomerShippingAccount { get; set; }
    public string? CustomerTermsAndConditions { get; set; }
    public string? CustomerCurrencyType { get; set; }
    // Pulled from the linked RFQ (via Quote → RFQItem → RFQ). 0 = Ex Warehouse, 1 = Ex Vendor, 2 = Ex Customer.
    public int? RfqExType { get; set; }

    public List<InvoiceItemResponse> Items { get; set; } = new();
}

public class InvoiceItemResponse
{
    public long Id { get; set; }
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal? Discount { get; set; }
    public decimal FinalPrice => Discount.HasValue ? TotalPrice - Discount.Value : TotalPrice;
    // Original unit price from the source quote item — used by the UI to compute/display
    // the per-unit discount when the user edits UnitPrice directly.
    public decimal? OriginalUnitPrice { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public long? QuoteItemId { get; set; }
    public string? RFQReference { get; set; }
    public string PartNumberName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Condition { get; set; }
    public string? CertName { get; set; }
    public string? LeadTime { get; set; }
}
