namespace Procument.Module.Purchasing.DTOs;

public class CreateILSProformaRequest
{
    public long ILSCustomerId { get; set; }
    public string? BillTo { get; set; }
    public string? ShipTo { get; set; }
    public string? Subject { get; set; }
    public string? CustomerPONumber { get; set; }
    public string? Notes { get; set; }
    public List<long> SourceQuoteIds { get; set; } = new();
    public List<ILSProformaItemRequest> Items { get; set; } = new();
}

public class ILSProformaItemRequest
{
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
    public long? ILSItemSerialId { get; set; }
    public string? SerialNumber { get; set; }
    public long? ILSItemId { get; set; }
    public long? SourceQuoteId { get; set; }
    public long? SourceQuoteItemId { get; set; }
}

public class UpdateILSProformaStatusRequest
{
    public string Status { get; set; } = string.Empty;
}

public class ILSProformaResponse
{
    public long Id { get; set; }
    public string PINumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long ILSCustomerId { get; set; }
    public string ILSCustomerName { get; set; } = string.Empty;
    public string? ILSCustomerCode { get; set; }
    public string? BillTo { get; set; }
    public string? ShipTo { get; set; }
    public string? Subject { get; set; }
    public string? CustomerPONumber { get; set; }
    public string? Notes { get; set; }
    public string? CustomerTermsAndConditions { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<ILSProformaItemResponse> Items { get; set; } = new();
}

public class ILSProformaItemResponse
{
    public long Id { get; set; }
    public long ILSProformaInvoiceId { get; set; }
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
    public long? ILSItemSerialId { get; set; }
    public string? SerialNumber { get; set; }
    public long? ILSItemId { get; set; }
    public long? SourceQuoteId { get; set; }
    public long? SourceQuoteItemId { get; set; }
}
