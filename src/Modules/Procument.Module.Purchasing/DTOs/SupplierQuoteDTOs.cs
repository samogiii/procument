namespace Procument.Module.Purchasing.DTOs;

public class SaveSupplierQuoteRequest
{
    public long? Id { get; set; }
    public long RFQItemId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public int Qty { get; set; }
    public decimal Price { get; set; }
    public string? Condition { get; set; }
    public string? Alt { get; set; }
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
    public int Qty { get; set; }
    public decimal Price { get; set; }
    public string? Condition { get; set; }
    public string? Alt { get; set; }
}
