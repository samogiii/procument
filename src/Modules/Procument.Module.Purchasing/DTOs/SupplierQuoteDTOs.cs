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
    public double Qty { get; set; }
    public decimal Price { get; set; }
    public string? Condition { get; set; }
    public string? Alt { get; set; }
    public string? Unit { get; set; }
    public string? LeadTime { get; set; }
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
}
