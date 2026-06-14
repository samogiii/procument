using Procument.Module.Sales.Entities;

namespace Procument.Module.Sales.DTOs;

public class CreateInvoiceRequest
{
    public long QuoteId { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? DeadlineDate { get; set; }
    public string? CustomerPONumber { get; set; }
    public DateTime? CustomerPODate { get; set; }
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
    public DateTime? DeadlineDate { get; set; }
    public string? CustomerPONumber { get; set; }
    public DateTime? CustomerPODate { get; set; }
    public string? Subject { get; set; }
    public string? PaymentStatus { get; set; }
    public decimal? PrepaymentPercent { get; set; }
    public decimal? Tax { get; set; }
    public decimal? Shipping { get; set; }
    public decimal? ProcessingFee { get; set; }
}

public class UpdateInvoiceItemDiscountRequest
{
    public long Id { get; set; }
    public decimal? FinalPrice { get; set; }  // user sets this; Discount = TotalPrice - FinalPrice
    // New: direct qty / unit-price edits. When provided, TotalPrice = Qty * UnitPrice,
    // and Discount is computed from QuoteItem.UnitPrice (the original quote price).
    public int? Qty { get; set; }
    public decimal? UnitPrice { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
}

public class UpdateInvoiceItemsRequest
{
    public List<UpdateInvoiceItemDiscountRequest> Items { get; set; } = new();
}

public class PrepaymentCheckResponse
{
    public string? PaymentStatus { get; set; }
    public decimal? PrepaymentPercent { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal RequiredAmount { get; set; }
    public decimal TotalPaid { get; set; }
    public bool IsSufficient { get; set; }
}

public class InvoiceResponse
{
    public long Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsCancelled { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? PaymentStatus { get; set; }
    public decimal? PrepaymentPercent { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? DeadlineDate { get; set; }
    public DateTime? PaidDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CustomerPONumber { get; set; }
    public DateTime? CustomerPODate { get; set; }
    public string? Subject { get; set; }

    // ─── PDF totals adjustments ───
    public decimal? Tax { get; set; }
    public decimal? Shipping { get; set; }
    public decimal? ProcessingFee { get; set; }

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
    public int? CustomerBase { get; set; }
    public string? CustomerContacts { get; set; }
    // Pulled from the linked RFQ (via Quote → RFQItem → RFQ). 0 = Ex Warehouse, 1 or 2 = Vendor/Customer.
    public int? RfqExType { get; set; }
    public long? DefaultDepositWalletId { get; set; }
    // Bank details from the selected deposit wallet (pre-resolved so the PDF generator doesn't need a separate API call)
    public string? WalletBankName { get; set; }
    public string? WalletBankAddress { get; set; }
    public string? WalletAccountNumber { get; set; }
    public string? WalletBeneficiaryName { get; set; }
    public string? WalletSwiftCode { get; set; }
    // Yuan pricing settings inherited from the source Quote
    public decimal? QuoteCoefYuan { get; set; }
    public decimal? QuoteExchangeRateYuan { get; set; }

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
    /// <summary>
    /// Original catalog part number name (from QuoteItem.PartNumber).
    /// When Alt is set, the customer was quoted the Alt — use Alt for display, keep this as reference.
    /// </summary>
    public string PartNumberName { get; set; } = string.Empty;
    /// <summary>
    /// Alternative part number quoted to the customer. When set, this is the "effective" part number
    /// shown in invoices, PDFs, and used as the POItem part number.
    /// </summary>
    public string? Alt { get; set; }
    /// <summary>Effective part number for display: Alt if set, otherwise PartNumberName.</summary>
    public string EffectivePartNumber => !string.IsNullOrWhiteSpace(Alt) ? Alt : PartNumberName;
    public string Description { get; set; } = string.Empty;
    public string? Condition { get; set; }
    public string? CertName { get; set; }
    public string? LeadTime { get; set; }
}

public record SetDefaultWalletRequest(long? WalletId);

public class UpdateInvoiceTotalsRequest
{
    public decimal? Tax { get; set; }
    public decimal? Shipping { get; set; }
    public decimal? ProcessingFee { get; set; }
}
