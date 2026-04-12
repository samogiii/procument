using Procument.Module.Sales.Enums;

namespace Procument.Module.Sales.DTOs;


// ──── Request DTOs ────

public class CreateQuoteRequest
{
    public long RFQId { get; set; }
    public DateTime? ValidUntil { get; set; }
    public decimal? FinalPrice { get; set; }
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

public class UpdateQuoteStatusRequest
{
    public string Status { get; set; } = string.Empty;
    public string? RejectionNote { get; set; }
}

// ──── Response DTOs ────

public class QuoteAssignedUserResponse
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class QuoteResponse
{
    public long Id { get; set; }
    public string QuoteNumber { get; set; } = string.Empty;
    public decimal? TotalAmount { get; set; }
    public string Status { get; set; } = "Draft";
    public DateTime? ValidUntil { get; set; }
    public DateTime CreatedAt { get; set; }
    public long RFQId { get; set; }
    public int? Type { get; set; }
    public string? TypeAdditional { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerCode { get; set; }
    public string? CustomerContactPerson { get; set; }
    public string? CustomerBillTo { get; set; }
    public string? CustomerShipTo { get; set; }
    public string? UserName { get; set; }
    public List<QuoteAssignedUserResponse> AssignedUsers { get; set; } = new();
    public string? RejectionNote { get; set; }
    public string? RFQName { get; set; }
    public decimal? FinalPrice { get; set; }
    public DateTime? SentAt { get; set; }
    public int? CustomerBase { get; set; }
    public List<QuoteItemResponse> Items { get; set; } = new();
}

public class QuoteItemResponse
{
    public long Id { get; set; }
    public string PartNumberName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long? PartNumberId { get; set; }
    public long? RFQItemId { get; set; }
    public long? ProcumentRecordId { get; set; }
    public string? Alt { get; set; }
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string? Condition { get; set; }
    public int? LeadTimeDays { get; set; }
    public string? LeadTime { get; set; }
    public string? Note { get; set; }
    public string? RFQReference { get; set; }
    public string? TagDate { get; set; }
    public string? CertName { get; set; }
    public decimal? BuyPrice { get; set; }
    public string? SupplierName { get; set; }
    public double? ShippingCost { get; set; }
    public decimal? FixPrice { get; set; }
}
