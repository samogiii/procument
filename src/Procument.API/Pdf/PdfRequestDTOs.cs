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

    // Text
    public string? Comments { get; set; }
    public string? Terms { get; set; }
    public string? FooterText { get; set; }
}

public class InvoicePdfItem
{
    public string? RfqReference { get; set; }
    public string? PartNumberName { get; set; }
    public string? Description { get; set; }
    public int Qty { get; set; }
    public string? Condition { get; set; }
    public string? CertName { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
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
    public decimal? Other { get; set; }

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
    public string? Description { get; set; }
    public int Qty { get; set; }
    public string? Condition { get; set; }
    public string? Certification { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
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

    // Customer
    public string? CustomerName { get; set; }
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
}

public class PackingListPdfItem
{
    public string? PartNumber { get; set; }
    public string? Description { get; set; }
    public int Qty { get; set; }
    public string? Condition { get; set; }
    public string? Certification { get; set; }
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
