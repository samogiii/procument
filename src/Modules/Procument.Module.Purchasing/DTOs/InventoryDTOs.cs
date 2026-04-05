namespace Procument.Module.Purchasing.DTOs;

public class SaveInventoryItemRequest
{
    public long? Id { get; set; }
    public long PartNumberId { get; set; }
    public string? Description { get; set; }
    public double Qty { get; set; }
    public long CompanyId { get; set; }
    public string? Condition { get; set; }
    public decimal? Price { get; set; }
}

public class InventoryItemResponse
{
    public long Id { get; set; }
    public long PartNumberId { get; set; }
    public string PartNumberName { get; set; } = "";
    public string? Description { get; set; }
    public double Qty { get; set; }
    public long CompanyId { get; set; }
    public string CompanyName { get; set; } = "";
    public string? Condition { get; set; }
    public decimal? Price { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class BulkImportInventoryRequest
{
    public List<BulkInventoryRow> Rows { get; set; } = new();
}

public class BulkInventoryRow
{
    public string PartNumberName { get; set; } = "";
    public string? Description { get; set; }
    public double Qty { get; set; }
    public string? CompanyName { get; set; }
    public string? Condition { get; set; }
    public decimal? Price { get; set; }
}
