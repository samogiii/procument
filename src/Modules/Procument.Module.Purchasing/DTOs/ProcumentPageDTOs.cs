namespace Procument.Module.Purchasing.DTOs;

public class ProcumentPageItemResponse
{
    public long RFQItemId { get; set; }
    public long RFQId { get; set; }
    public string RFQName { get; set; } = string.Empty;
    public string RFQStatus { get; set; } = "Open";
    public string PartNumberName { get; set; } = string.Empty;
    public long PartNumberId { get; set; }
    public string? Description { get; set; }
    public double Qty { get; set; }
    public string? Condition { get; set; }
    public string? Unit { get; set; }
    public string? Priority { get; set; }
    public string? Note { get; set; }
    public bool IsHighlighted { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerCode { get; set; }
    public int? CustomerBase { get; set; }
    public DateTime LeadTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<ProcumentPageUserResponse> AssignedUsers { get; set; } = new();
    public List<SupplierQuoteResponse> SupplierQuotes { get; set; } = new();
    public List<ProcumentPageAltResponse> Alternatives { get; set; } = new();
}

public class ProcumentPageAltResponse
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class ProcumentPageUserResponse
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

// ── Supplier Suggestions ──

public class SupplierSuggestionsResponse
{
    public List<KnownSupplierDto> KnownSuppliers { get; set; } = new();
    public List<RecentSupplierQuoteDto> RecentQuotes { get; set; } = new();
}

public class KnownSupplierDto
{
    public long SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
}

public class RecentSupplierQuoteDto
{
    public long SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string SupplierDependency { get; set; } = "Normal";
    public double Qty { get; set; }
    public decimal Price { get; set; }
    public string? Condition { get; set; }
    public string? Alt { get; set; }
    public string? Unit { get; set; }
    public string? LeadTime { get; set; }
    public string? CertName { get; set; }
    public DateOnly? TagDate { get; set; }
    public double? ShippingCost { get; set; }
    public string? ShippingPoint { get; set; }
    public string? Note { get; set; }
    public string? MyNotes { get; set; }
    public long RFQId { get; set; }
    public string RFQName { get; set; } = string.Empty;
}
