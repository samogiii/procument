namespace Procument.Module.Purchasing.DTOs;

// ─── ILS Customer DTOs ───

public class ILSCustomerDto
{
    public string Name { get; set; } = string.Empty;
    public string? CustomerCode { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? ContactPerson { get; set; }
    public string? Address { get; set; }
    public string? Description { get; set; }
    public string? BillTo { get; set; }
    public string? ShipTo { get; set; }
    public string? TermsAndConditions { get; set; }
    public string? Website { get; set; }
    public string? Country { get; set; }
    public string? ShippingAccount { get; set; }
}

public class ILSCustomerResponse
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? CustomerCode { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? ContactPerson { get; set; }
    public string? Address { get; set; }
    public string? Description { get; set; }
    public string? BillTo { get; set; }
    public string? ShipTo { get; set; }
    public string? TermsAndConditions { get; set; }
    public string? Website { get; set; }
    public string? Country { get; set; }
    public string? ShippingAccount { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ─── ILS Quote DTOs ───

public class CreateILSQuoteRequest
{
    public long ILSCustomerId { get; set; }
    public string? RfqReference { get; set; }
    public string? Notes { get; set; }
    public string? BillTo { get; set; }
    public string? ShipTo { get; set; }
    public List<ILSQuoteItemRequest> Items { get; set; } = new();
}

public class ILSQuoteItemRequest
{
    public long? Id { get; set; }
    public long PartNumberId { get; set; }
    public string? PartNumberName { get; set; }
    public string? AltPartNumber { get; set; }
    public string? Condition { get; set; }
    public string? CertName { get; set; }
    public double Qty { get; set; }
    public decimal SellPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string? LeadTime { get; set; }
    public string? Notes { get; set; }
    public long? ILSItemId { get; set; }
    public long? ILSItemSerialId { get; set; }
    public string? SerialNumber { get; set; }
    public decimal? BasePrice { get; set; }
    public decimal? Coef { get; set; }
}

public class ILSQuoteResponse
{
    public long Id { get; set; }
    public string QuoteNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long ILSCustomerId { get; set; }
    public string ILSCustomerName { get; set; } = string.Empty;
    public string? ILSCustomerCode { get; set; }
    public string? RfqReference { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }
    public string? BillTo { get; set; }
    public string? ShipTo { get; set; }
    public string? CustomerTermsAndConditions { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<ILSQuoteItemResponse> Items { get; set; } = new();
}

public class ILSQuoteItemResponse
{
    public long Id { get; set; }
    public long ILSQuoteId { get; set; }
    public long PartNumberId { get; set; }
    public string PartNumberName { get; set; } = string.Empty;
    public string? AltPartNumber { get; set; }
    public string? Condition { get; set; }
    public string? CertName { get; set; }
    public double Qty { get; set; }
    public decimal SellPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string? LeadTime { get; set; }
    public string? Notes { get; set; }
    public long? ILSItemId { get; set; }
    public long? ILSItemSerialId { get; set; }
    public string? SerialNumber { get; set; }
    public decimal? BasePrice { get; set; }
    public decimal? Coef { get; set; }
}

public class UpdateILSQuoteStatusRequest
{
    public string Status { get; set; } = string.Empty;
}
