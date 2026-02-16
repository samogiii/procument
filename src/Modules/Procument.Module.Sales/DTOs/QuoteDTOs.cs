namespace Procument.Module.Sales.DTOs;

// ──── Request DTOs ────

public class CreateQuoteRequest
{
    public long RFQId { get; set; }
    public DateTime? ValidUntil { get; set; }
    public List<CreateQuoteItemRequest> Items { get; set; } = new();
}

public class CreateQuoteItemRequest
{
    public long RFQItemId { get; set; }
    public long? ProcumentRecordId { get; set; }
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public string? Condition { get; set; }
    public string? Alt { get; set; }
    public int? LeadTimeDays { get; set; }
}

// ──── Response DTOs ────

public class QuoteResponse
{
    public long Id { get; set; }
    public string QuoteNumber { get; set; } = string.Empty;
    public decimal? TotalAmount { get; set; }
    public string Status { get; set; } = "Draft";
    public DateTime? ValidUntil { get; set; }
    public DateTime CreatedAt { get; set; }
    public long RFQId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public List<QuoteItemResponse> Items { get; set; } = new();
}

public class QuoteItemResponse
{
    public long Id { get; set; }
    public string PartNumberName { get; set; } = string.Empty;
    public long? PartNumberId { get; set; }
    public long? RFQItemId { get; set; }
    public string? Alt { get; set; }
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string? Condition { get; set; }
    public int? LeadTimeDays { get; set; }
}
