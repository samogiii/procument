namespace Procument.Module.Purchasing.DTOs;

public class SaveCapListItemRequest
{
    public long? Id { get; set; }
    public long? PartNumberId { get; set; }
    public string? PartNumberName { get; set; }
    public string? Description { get; set; }
    public long? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public bool IsRepair { get; set; } = false;
    public long? ProcumentRecordId { get; set; }
}

public class CapListItemResponse
{
    public long Id { get; set; }
    public long PartNumberId { get; set; }
    public string PartNumberName { get; set; } = "";
    public string? Description { get; set; }
    public long CompanyId { get; set; }
    public string CompanyName { get; set; } = "";
    public bool IsRepair { get; set; }
    public string? Condition { get; set; }
    public long? ProcumentRecordId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class BulkImportCapListRequest
{
    public List<BulkCapListRow> Rows { get; set; } = new();
}

public class BulkCapListRow
{
    public string PartNumberName { get; set; } = "";
    public string? Description { get; set; }
    public string? CompanyName { get; set; }
    public bool IsRepair { get; set; }
}

public class ARShopForCapListResponse
{
    public long ProcumentRecordId { get; set; }
    public long PartNumberId { get; set; }
    public string PartNumberName { get; set; } = "";
    public string? AltPartNumber { get; set; }
    public long SupplierId { get; set; }
    public string SupplierName { get; set; } = "";
    public decimal? Price { get; set; }
    public decimal? FixPrice { get; set; }
    public double? Qty { get; set; }
    public string? Condition { get; set; }
    public string? RFQName { get; set; }
}

public class BulkImportResult
{
    public int Created { get; set; }
    public int Skipped { get; set; }
    public List<string> Errors { get; set; } = new();
}
