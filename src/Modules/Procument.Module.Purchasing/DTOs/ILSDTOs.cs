namespace Procument.Module.Purchasing.DTOs;

public class SaveILSItemRequest
{
    public long? Id { get; set; }
    public long? PartNumberId { get; set; }
    public string? PartNumberName { get; set; }
    public string? Description { get; set; }
    public string? AltPartNumber { get; set; }
    public decimal Price { get; set; }
    public double Qty { get; set; }
    public string? Condition { get; set; }
    public DateOnly? TagDate { get; set; }
    public string? CertName { get; set; }
    public string? LeadTime { get; set; }
    public long? ProcumentRecordId { get; set; }
}

public class ILSItemResponse
{
    public long Id { get; set; }
    public long PartNumberId { get; set; }
    public string PartNumberName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? AltPartNumber { get; set; }
    public decimal Price { get; set; }
    public double Qty { get; set; }
    public string? Condition { get; set; }
    public DateOnly? TagDate { get; set; }
    public string? CertName { get; set; }
    public string? LeadTime { get; set; }
    public long? ProcumentRecordId { get; set; }
    public int SerialCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SaveILSSerialRequest
{
    public long? Id { get; set; }
    public long ILSItemId { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string? LeadTime { get; set; }
    public string? CertText { get; set; }
    public decimal? Price { get; set; }
    public string? Location { get; set; }
    public string? Condition { get; set; }
    public DateOnly? TagDate { get; set; }
    public string? Notes { get; set; }
}

public class ILSSerialResponse
{
    public long Id { get; set; }
    public long ILSItemId { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string? LeadTime { get; set; }
    public string? CertText { get; set; }
    public bool HasCertImage { get; set; }
    public string? CertImageOriginalName { get; set; }
    public bool HasPartImage { get; set; }
    public string? PartImageOriginalName { get; set; }
    public decimal? Price { get; set; }
    public string? Location { get; set; }
    public string? Condition { get; set; }
    public DateOnly? TagDate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class BulkImportILSRequest
{
    public List<BulkILSRow> Rows { get; set; } = new();
}

public class BulkILSRow
{
    public string PartNumberName { get; set; } = "";
    public string? Description { get; set; }
    public string? AltPartNumber { get; set; }
    public decimal Price { get; set; }
    public double Qty { get; set; }
    public string? Condition { get; set; }
    public string? TagDate { get; set; }
    public string? CertName { get; set; }
    public string? LeadTime { get; set; }
}

public class ARShopSuggestionResponse
{
    public long ProcumentRecordId { get; set; }
    public long RFQItemId { get; set; }
    public long RFQId { get; set; }
    public string RFQName { get; set; } = string.Empty;
    public long PartNumberId { get; set; }
    public string PartNumberName { get; set; } = string.Empty;
    public string? AltPartNumber { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? FixPrice { get; set; }
    public double Qty { get; set; }
    public string? Condition { get; set; }
    public string? CertName { get; set; }
    public DateOnly? TagDate { get; set; }
    public string? LeadTime { get; set; }
    public double? ShippingCost { get; set; }
    public string? ShippingPoint { get; set; }
}
