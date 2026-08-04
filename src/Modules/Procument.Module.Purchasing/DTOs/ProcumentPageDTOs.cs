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
    /// <summary>True when TagDate is older than 14 days — Price is 0 and should not be displayed.</summary>
    public bool PriceHidden { get; set; }
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

// ── Part History (read-only audit of everything ever recorded for a part) ──

public class PartHistoryResponse
{
    public long PartNumberId { get; set; }
    public string PartNumberName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? PartCreatedAt { get; set; }
    /// <summary>Names of alternative-linked part numbers whose history is merged in.</summary>
    public List<string> RelatedPartNumbers { get; set; } = new();
    public int TotalRfqCount { get; set; }
    public int TotalRecordCount { get; set; }
    /// <summary>True when the record list was capped — summary counts still reflect everything.</summary>
    public bool Truncated { get; set; }
    public List<PartHistoryExpertDto> Experts { get; set; } = new();
    public List<PartHistorySupplierDto> Suppliers { get; set; } = new();
    public List<PartHistoryRecordDto> Records { get; set; } = new();
}

public class PartHistoryExpertDto
{
    public long UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? Role { get; set; }
    /// <summary>RFQs containing this part that the user owns.</summary>
    public int OwnedRfqCount { get; set; }
    /// <summary>RFQs containing this part the user was granted access to.</summary>
    public int AssignedRfqCount { get; set; }
    /// <summary>Supplier cost rows for this part the user entered.</summary>
    public int RecordCount { get; set; }
    public DateTime? FirstActivity { get; set; }
    public DateTime? LastActivity { get; set; }
}

public class PartHistorySupplierDto
{
    public long SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? Dependency { get; set; }
    public string Status { get; set; } = "Approved";
    /// <summary>When the supplier itself was first created in the system.</summary>
    public DateTime? SupplierCreatedAt { get; set; }
    /// <summary>When the supplier was linked to this part number.</summary>
    public DateTime? LinkedAt { get; set; }
    public bool IsLinked { get; set; }
    public int RecordCount { get; set; }
    public DateTime? FirstQuotedAt { get; set; }
    public DateTime? LastQuotedAt { get; set; }
    public decimal? LastPrice { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public List<string> Conditions { get; set; } = new();
    public List<string> Certs { get; set; } = new();
}

public class PartHistoryRecordDto
{
    public long Id { get; set; }
    public long SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? SupplierDependency { get; set; }
    public string SupplierStatus { get; set; } = "Approved";
    public DateTime? SupplierCreatedAt { get; set; }
    /// <summary>"Procument" for a supplier row, "Shop" for a nested shop row.</summary>
    public string Type { get; set; } = "Procument";
    public string? Condition { get; set; }
    public string? Alt { get; set; }
    public double Qty { get; set; }
    public string? Unit { get; set; }
    public decimal Price { get; set; }
    public string? CertName { get; set; }
    public DateOnly? TagDate { get; set; }
    public string? LeadTime { get; set; }
    public double? ShippingCost { get; set; }
    public string? ShippingPoint { get; set; }
    public string? Note { get; set; }
    public string? MyNotes { get; set; }
    /// <summary>When this cost row was first recorded.</summary>
    public DateTime CreatedAt { get; set; }
    /// <summary>Last time this cost row was edited.</summary>
    public DateTime? UpdatedAt { get; set; }
    public long? EnteredByUserId { get; set; }
    public string? EnteredByName { get; set; }
    public long RFQId { get; set; }
    public string RFQName { get; set; } = string.Empty;
    public string RFQStatus { get; set; } = "Open";
    public DateTime RFQCreatedAt { get; set; }
    public string PartNumberName { get; set; } = string.Empty;
    public string? RFQOwnerName { get; set; }
    public List<string> AssignedUsers { get; set; } = new();
}
