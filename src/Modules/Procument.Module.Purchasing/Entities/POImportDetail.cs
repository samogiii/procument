using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Entities;

public class POImportDetail : BaseEntity
{
    public string? BankName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankAddress { get; set; }
    public string? BankCity { get; set; }
    public string? BankCountry { get; set; }
    public string? FedExAccount { get; set; }
    public string? CourierName { get; set; }
    public string? ShippingMethod { get; set; }  // Air, Sea, Ground, Express
    public string? Incoterms { get; set; }       // FOB, CIF, EXW, DDP, etc.
    public string? Notes { get; set; }
    public decimal? Wirefee { get; set; }
    public string? SwiftCode { get; set; }
    public string? ABA { get; set; }

    // Foreign key
    public long PurchaseOrderId { get; set; }

    // Navigation
    public PurchaseOrder PurchaseOrder { get; set; } = null!;
}
