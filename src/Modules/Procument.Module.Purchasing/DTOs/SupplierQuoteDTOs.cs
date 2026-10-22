namespace Procument.Module.Purchasing.DTOs;

public class SaveSupplierQuoteRequest
{
    public long? Id { get; set; }
    public long RFQItemId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public double Qty { get; set; }
    public decimal Price { get; set; }
    public string? Condition { get; set; }
    public string? Alt { get; set; }
    public string? Unit { get; set; }
    public string? LeadTime { get; set; }
    public string? SellLeadTime { get; set; }
    public double? Coef_1 { get; set; }
    public double? Coef_2 { get; set; }
    public double? Coef_3 { get; set; }
    public double? ShippingCost { get; set; }
    public string? ShippingPoint { get; set; }
    public string? CertName { get; set; }
    public double? UnitPrice { get; set; }
    public double? TotalPrice { get; set; }
    public DateOnly? TagDate { get; set; }
    public string? Note { get; set; }
    public string? MyNotes { get; set; }
    public string Type { get; set; } = "Procument";
    public decimal? FixPrice { get; set; }
    public long? ParentProcumentId { get; set; }
    public bool IsCertificated { get; set; }
    /// <summary>User-typed cert references. Null = not sent by this client, keep what is stored.</summary>
    public List<string>? CustomCertReferences { get; set; }
    /// <summary>The Our Stock lot this row offers (from the RFQ availability chip).</summary>
    public long? SourceStockItemId { get; set; }
    /// <summary>Our company (CompanyPreset) for this quote. Null = not sent by this client, keep what is stored; 0 = clear.</summary>
    public long? CompanyPresetId { get; set; }
    /// <summary>Supplier email used for this RFQ item. Null = not sent by this client, keep what is stored; '' = clear.</summary>
    public string? SupplierEmail { get; set; }

    // Quote details. Each: null = not sent by this client, keep what is stored; '' = clear.
    public string? SerialNumber { get; set; }
    public string? TraceTo { get; set; }
    public string? TagType { get; set; }
    public string? PriceType { get; set; }
    public string? TaggedBy { get; set; }
    public string? MoQ { get; set; }
    public string? Warranty { get; set; }
    public string? CR { get; set; }
}

public class BulkSaveQuotesRequest
{
    public List<SaveSupplierQuoteRequest> Quotes { get; set; } = new();
}

public class SupplierQuoteResponse
{
    public long Id { get; set; }
    public long RFQItemId { get; set; }
    public long SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string SupplierStatus { get; set; } = "Approved";
    public string SupplierDependency { get; set; } = "Normal";
    public double Qty { get; set; }
    public decimal Price { get; set; }
    /// <summary>True when the cost is stale — i.e. the RFQ it was gathered for is older than
    /// 14 days. When true, Price is 0 and should not be displayed. This is based on cost age,
    /// not the part's Tag Date (which can legitimately be old on a freshly-quoted part).</summary>
    public bool PriceHidden { get; set; }
    public string? Condition { get; set; }
    public string? Alt { get; set; }
    public string? Unit { get; set; }
    public string? LeadTime { get; set; }
    public string? SellLeadTime { get; set; }
    public double? Coef_1 { get; set; }
    public double? Coef_2 { get; set; }
    public double? Coef_3 { get; set; }
    public double? ShippingCost { get; set; }
    public string? ShippingPoint { get; set; }
    public string? CertName { get; set; }
    public double? UnitPrice { get; set; }
    public double? TotalPrice { get; set; }
    public DateOnly? TagDate { get; set; }
    public string? Note { get; set; }
    public string? MyNotes { get; set; }
    public bool IsCertificated { get; set; }
    public string Type { get; set; } = "Procument";
    public decimal? FixPrice { get; set; }
    public long? ParentProcumentId { get; set; }
    public long? SourceStockItemId { get; set; }
    public long? CompanyPresetId { get; set; }
    public string? CompanyPresetName { get; set; }
    public string? SupplierEmail { get; set; }
    /// <summary>Sender address of the accepted Email Quotes line this row came from.</summary>
    public string? SourceEmail { get; set; }
    /// <summary>The catalog supplier's main email.</summary>
    public string? SupplierMainEmail { get; set; }
    public string? SerialNumber { get; set; }
    public string? TraceTo { get; set; }
    public string? TagType { get; set; }
    public string? PriceType { get; set; }
    public string? TaggedBy { get; set; }
    public string? MoQ { get; set; }
    public string? Warranty { get; set; }
    public string? CR { get; set; }
    /// <summary>The expert who imported this quote. Only filled for SuperAdmin requests.</summary>
    public long? ExpertUserId { get; set; }
    public string? ExpertName { get; set; }
    public int SortOrder { get; set; }
    public int CertificateCount { get; set; }
    public List<string> CertReferences { get; set; } = new();
    public List<string> CustomCertReferences { get; set; } = new();
    public List<SupplierQuoteResponse> ShopRecords { get; set; } = new();
}

public class UpdateSupplierQuotesOrderRequest
{
    public List<SupplierQuoteOrderEntry> Items { get; set; } = new();
}

public class SupplierQuoteCertificateResponse
{
    public long Id { get; set; }
    public long SupplierQuoteId { get; set; }
    public string? PartNumberName { get; set; }
    public string? SupplierName { get; set; }
    public string? Alt { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string? MimeType { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; }
    public string? UploadedByName { get; set; }
}

public class SupplierQuoteOrderEntry
{
    public long Id { get; set; }
    public int SortOrder { get; set; }
}
