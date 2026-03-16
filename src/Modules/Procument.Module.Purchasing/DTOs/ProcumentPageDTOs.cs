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
    public DateTime LeadTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<ProcumentPageUserResponse> AssignedUsers { get; set; } = new();
    public List<SupplierQuoteResponse> SupplierQuotes { get; set; } = new();
}

public class ProcumentPageUserResponse
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
