namespace Procument.API.Pdf;

// ──────────────────────────────────────────────────────
// PROFORMA INVOICE
// ──────────────────────────────────────────────────────
public class InvoicePdfRequest
{
    // Company (from preset)
    public string? CompanyName { get; set; }
    public string? CompanyLocation { get; set; }
    public string? CompanyPhone { get; set; }
    public string? CompanyWebsite { get; set; }
    public string? CompanyEmail { get; set; }
    public string? LogoBase64 { get; set; }

    // Theme (from company preset)
    public string? PrimaryColor { get; set; }
    public string? AccentColor { get; set; }

    // Document meta
    public string? InvoiceNumber { get; set; }
    public string? InvoiceDate { get; set; }
    public string? DueDate { get; set; }
    public string? Status { get; set; }
    public string? CustomerPONumber { get; set; }
    public string? Currency { get; set; }
    public string? CurrencySymbol { get; set; }

    // Customer
    public string? CustomerName { get; set; }
    public string? CustomerBillToName { get; set; }
    public string? CustomerShipToName { get; set; }
    public string? CustomerContactPerson { get; set; }
    public string? CustomerBillTo { get; set; }
    public string? CustomerBillToEmail { get; set; }
    public string? CustomerBillToPhone { get; set; }
    public string? CustomerShipTo { get; set; }
    public string? CustomerShipToContactPerson { get; set; }
    public string? CustomerShipToEmail { get; set; }
    public string? CustomerShipToPhone { get; set; }
    public string? CustomerShipToAccount { get; set; }

    // Bank Details
    public string? BeneficiaryName { get; set; }
    public string? BeneficiaryAddress { get; set; }
    public string? BankName { get; set; }
    public string? BankAddress { get; set; }
    public string? BankAccount { get; set; }
    public string? SwiftCode { get; set; }

    // Items
    public List<InvoicePdfItem>? Items { get; set; }

    // Totals
    public decimal? Subtotal { get; set; }
    public decimal? Tax { get; set; }
    public decimal? Shipping { get; set; }
    public decimal? Other { get; set; }

    // Display options
    public bool ShowDiscount { get; set; } = true;

    // Text
    public string? Comments { get; set; }
    public string? Terms { get; set; }
    public string? FooterText { get; set; }
}

public class InvoicePdfItem
{
    public string? RfqReference { get; set; }
    public string? PartNumberName { get; set; }
    /// <summary>Alternative part number quoted to the customer. When set, it is shown as the effective PN.</summary>
    public string? Alt { get; set; }
    public string? Description { get; set; }
    public int Qty { get; set; }
    public string? Condition { get; set; }
    public string? CertName { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal? Discount { get; set; }
    public string? DeliveryDate { get; set; }
    public string? LeadTime { get; set; }
}

// ──────────────────────────────────────────────────────
// PURCHASE ORDER
// ──────────────────────────────────────────────────────
public class PurchaseOrderPdfRequest
{
    // Company
    public string? CompanyName { get; set; }
    public string? CompanyLocation { get; set; }
    public string? CompanyPhone { get; set; }
    public string? CompanyWebsite { get; set; }
    public string? CompanyEmail { get; set; }
    public string? LogoBase64 { get; set; }

    // Theme (from company preset)
    public string? PrimaryColor { get; set; }
    public string? AccentColor { get; set; }

    // Document meta
    public string? PoNumber { get; set; }
    public string? PoDate { get; set; }
    public string? OrderedBy { get; set; }
    public string? Status { get; set; }
    public string? Currency { get; set; }
    public string? CurrencySymbol { get; set; }

    // Purchase From (Supplier)
    public string? PurchaseFromName { get; set; }
    public string? PurchaseFromAddress { get; set; }
    public string? PurchaseFromPhone { get; set; }
    public string? PurchaseFromEmail { get; set; }

    // Vendor
    public string? VendorName { get; set; }
    public string? VendorAddress { get; set; }
    public string? VendorPhone { get; set; }
    public string? VendorEmail { get; set; }

    // Deliver To
    public string? DeliverToName { get; set; }
    public string? DeliverToAddress { get; set; }
    public string? DeliverToPhone { get; set; }
    public string? DeliverToEmail { get; set; }

    // Shipping Info
    public string? ShippingMethod { get; set; }
    public string? Incoterms { get; set; }
    public string? FedExAccount { get; set; }
    public string? ServicePriority { get; set; }

    // Items
    public List<PurchaseOrderPdfItem>? Items { get; set; }

    // Totals
    public decimal? Subtotal { get; set; }
    public decimal? Tax { get; set; }
    public decimal? TotalShipping { get; set; }
    //public decimal? Other { get; set; }
    /// <summary>Flat processing-fee line on the totals block (PO-only).</summary>
    public decimal? ProcessingFee { get; set; }

    // Text
    public string? Comments { get; set; }
    public string? Terms { get; set; }
    public string? FooterText { get; set; }
}

public class PurchaseOrderPdfItem
{
    public string? PartNumber { get; set; }
    public string? Description { get; set; }
    public int Qty { get; set; }
    public string? Condition { get; set; }
    public string? Certification { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal ShippingCost { get; set; }
    public string? Note { get; set; }
}

// ──────────────────────────────────────────────────────
// FINAL INVOICE
// ──────────────────────────────────────────────────────
public class FinalInvoicePdfRequest
{
    // Company
    public string? CompanyName { get; set; }
    public string? CompanyLocation { get; set; }
    public string? CompanyPhone { get; set; }
    public string? CompanyWebsite { get; set; }
    public string? CompanyEmail { get; set; }
    public string? LogoBase64 { get; set; }

    // Theme (from company preset)
    public string? PrimaryColor { get; set; }
    public string? AccentColor { get; set; }

    // Document meta
    public string? InvoiceNumber { get; set; }
    public string? InvoiceDate { get; set; }
    public string? DueDate { get; set; }
    public string? ProformaRef { get; set; }
    public string? CustomerPONumber { get; set; }
    public string? Currency { get; set; }
    public string? CurrencySymbol { get; set; }

    // Customer
    public string? CustomerName { get; set; }
    public string? CustomerBillToName { get; set; }
    public string? CustomerShipToName { get; set; }
    public string? CustomerContactPerson { get; set; }
    public string? CustomerBillTo { get; set; }
    public string? CustomerBillToEmail { get; set; }
    public string? CustomerBillToPhone { get; set; }
    public string? CustomerBillToContactPerson { get; set; }
    public string? CustomerShipTo { get; set; }
    public string? CustomerShipToContactPerson { get; set; }
    public string? CustomerShipToEmail { get; set; }
    public string? CustomerShipToPhone { get; set; }
    public string? CustomerShipToAccount { get; set; }

    // Bank Details
    public string? BeneficiaryName { get; set; }
    public string? BeneficiaryAddress { get; set; }
    public string? BankName { get; set; }
    public string? BankAddress { get; set; }
    public string? BankAccount { get; set; }
    public string? SwiftCode { get; set; }

    // Shipping
    public string? ShippingMethod { get; set; }
    public decimal? ShippingCost { get; set; }

    // Items
    public List<FinalInvoicePdfItem>? Items { get; set; }

    // Totals
    public decimal? Subtotal { get; set; }
    public decimal? Tax { get; set; }
    public decimal? Other { get; set; }

    // Text
    public string? Comments { get; set; }
    public string? Terms { get; set; }
    public string? FooterText { get; set; }
}

public class FinalInvoicePdfItem
{
    public string? RfqReference { get; set; }
    public string? PartNumber { get; set; }
    /// <summary>Alternative part number quoted to the customer. When set, it is shown as the effective PN.</summary>
    public string? Alt { get; set; }
    public string? Description { get; set; }
    public int Qty { get; set; }
    public string? Condition { get; set; }
    public string? Certification { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal? Discount { get; set; }
    public string? TrackNumber { get; set; }
    public string? Carrier { get; set; }
}

// ──────────────────────────────────────────────────────
// PACKING LIST
// ──────────────────────────────────────────────────────
public class PackingListPdfRequest
{
    // Company
    public string? CompanyName { get; set; }
    public string? CompanyLocation { get; set; }
    public string? CompanyPhone { get; set; }
    public string? CompanyWebsite { get; set; }
    public string? CompanyEmail { get; set; }
    public string? LogoBase64 { get; set; }

    // Theme (from company preset)
    public string? PrimaryColor { get; set; }
    public string? AccentColor { get; set; }

    // Document meta
    public string? InvoiceNumber { get; set; }
    public string? InvoiceDate { get; set; }
    public string? CustomerPONumber { get; set; }
    public string? ProformaRef { get; set; }

    // Customer
    public string? CustomerName { get; set; }
    public string? CustomerBillToName { get; set; }
    public string? CustomerShipToName { get; set; }
    public string? CustomerContactPerson { get; set; }
    public string? CustomerBillTo { get; set; }
    public string? CustomerBillToEmail { get; set; }
    public string? CustomerBillToPhone { get; set; }
    public string? CustomerBillToContactPerson { get; set; }
    public string? CustomerShipTo { get; set; }
    public string? CustomerShipToContactPerson { get; set; }
    public string? CustomerShipToEmail { get; set; }
    public string? CustomerShipToPhone { get; set; }
    public string? CustomerShipToAccount { get; set; }

    // Items
    public List<PackingListPdfItem>? Items { get; set; }

    // Packages / shipping dimensions (global, not per-item)
    public List<PackingListPackage>? Packages { get; set; }
}

public class PackingListPdfItem
{
    public string? PartNumber { get; set; }
    public string? Description { get; set; }
    public int Qty { get; set; }
    public string? Condition { get; set; }
    public string? Certification { get; set; }
}

public class PackingListPackage
{
    public string? Weight { get; set; }
    public string? Dimensions { get; set; }
}

// ──────────────────────────────────────────────────────
// RFQ
// ──────────────────────────────────────────────────────
public class RfqPdfRequest
{
    public string? HeaderText { get; set; }
    public string? FooterText { get; set; }
    public string? Terms { get; set; }
    public string? LogoBase64 { get; set; }

    // Theme (from company preset)
    public string? PrimaryColor { get; set; }
    public string? AccentColor { get; set; }

    public long? RfqId { get; set; }
    public string? RfqName { get; set; }
    public string? RfqDate { get; set; }

    public List<RfqPdfItem>? Items { get; set; }
}

public class RfqPdfItem
{
    public string? PartNumberName { get; set; }
    public string? Description { get; set; }
    public int Qty { get; set; }
    public string? Condition { get; set; }
    public string? Remark { get; set; }
    public List<string>? Alternatives { get; set; }
}

// ──────────────────────────────────────────────────────
// PAYMENT REQUEST (PR) - formerly DP
// ──────────────────────────────────────────────────────
public class PaymentRequestPdfRequest
{
    // Company
    public string? CompanyName { get; set; }
    public string? CompanyLocation { get; set; }
    public string? CompanyPhone { get; set; }
    public string? CompanyWebsite { get; set; }
    public string? CompanyEmail { get; set; }
    public string? LogoBase64 { get; set; }

    // Theme
    public string? PrimaryColor { get; set; }
    public string? AccentColor { get; set; }

    // Meta
    public string? PrNumber { get; set; } // e.g. PR01501
    public string? DocumentDate { get; set; }
    public string? PoNumber { get; set; }
    public string? SupplierName { get; set; }
    public string? Currency { get; set; }
    public string? CurrencySymbol { get; set; }
    public string? Status { get; set; }

    // Our Company Bank Details (paying company)
    public string? CompanyPayingFrom { get; set; }
    public string? OurBeneficiaryName { get; set; }
    public string? OurAccountNumber { get; set; }
    public string? OurBankName { get; set; }
    public string? OurSwiftCode { get; set; }
    public string? OurBankAddress { get; set; }
    public string? OurCompanyAddress { get; set; }

    // Supplier Bank Details (receiving company)
    public string? CompanyPayingTo { get; set; }
    public string? AccountNumber { get; set; }
    public string? BankName { get; set; }
    public string? SwiftCode { get; set; }
    public string? ABA { get; set; }
    public string? CompanyAddress { get; set; }
    public string? BankAddress { get; set; }

    // Items
    public List<PaymentRequestPdfItem>? Items { get; set; }

    // Totals
    public decimal ItemsTotal { get; set; }
    public decimal WireFee { get; set; }
    public decimal GrandTotal { get; set; }

    /// <summary>
    /// Bank fee option:
    /// "OurCompanyAll"   → 本公司支付所有的银行费用 / Our company pays all bank fees.
    /// "OurCompanyLocal" → 本公司支付本地银行费用，受款人支付海外银行费用。/ Our company pays local bank fees; the recipient pays overseas bank fees.
    /// "RecipientAll"    → 受款公司支付所有的银行费用 / The recipient company pays all bank fees.
    /// </summary>
    public string? BankFeeOption { get; set; }

    // Text
    public string? FooterText { get; set; }
}

public class PaymentRequestPdfItem
{
    public string? PartNumber { get; set; }
    public string? Description { get; set; }
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}

// ──────────────────────────────────────────────────────
// DASTURPARDAKHT (DP)
// ──────────────────────────────────────────────────────
public class DpPdfRequest
{
    // Company
    public string? CompanyName { get; set; }
    public string? CompanyLocation { get; set; }
    public string? CompanyPhone { get; set; }
    public string? CompanyWebsite { get; set; }
    public string? CompanyEmail { get; set; }
    public string? LogoBase64 { get; set; }

    // Theme
    public string? PrimaryColor { get; set; }
    public string? AccentColor { get; set; }

    // Meta
    public string? PoNumber { get; set; }
    public string? DocumentDate { get; set; }
    public string? SupplierName { get; set; }
    public string? Currency { get; set; }
    public string? CurrencySymbol { get; set; }
    public string? CompanyPresetName { get; set; }

    // Bank Details (supplier's)
    public string? BankName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankAddress { get; set; }
    public string? BankCity { get; set; }
    public string? BankCountry { get; set; }
    public string? SwiftCode { get; set; }
    public string? Notes { get; set; }

    // Items
    public List<DpPdfItem>? Items { get; set; }

    // Totals
    public decimal? GrandTotal { get; set; }

    // Footer
    public string? FooterText { get; set; }
}

public class DpPdfItem
{
    public string? PartNumber { get; set; }
    public int Qty { get; set; }
    public string? PoSupplier { get; set; }
    public decimal? QuotePrice { get; set; }
    public decimal PoPrice { get; set; }
    public decimal PoTotal { get; set; }
}
