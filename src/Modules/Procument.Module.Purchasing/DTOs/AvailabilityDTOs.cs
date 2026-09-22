namespace Procument.Module.Purchasing.DTOs;

public class PartAvailabilityRequest
{
    public List<long> PartNumberIds { get; set; } = new();
}

public class AvailabilityRecord
{
    public string Label { get; set; } = "";       // display name (supplier or company)
    public decimal? Price { get; set; }
    public double? Qty { get; set; }
    public string? Condition { get; set; }
    public string? CertName { get; set; }
    public string? LeadTime { get; set; }
    public string? AltPartNumber { get; set; }
    public string? TagDate { get; set; }
}

/// <summary>
/// Module-neutral availability row returned by an external Purchasing availability source.
/// </summary>
public sealed class PartAvailabilitySourceRecord : AvailabilityRecord
{
    public long PartNumberId { get; set; }
    public long? StockItemId { get; set; }
    public bool IsIncoming { get; set; }
}

public class PartAvailabilityResponse
{
    public long PartNumberId { get; set; }
    public List<AvailabilityRecord> InventoryRecords { get; set; } = new();
    public List<AvailabilityRecord> CapListRecords { get; set; } = new();
    public List<AvailabilityRecord> ILSRecords { get; set; } = new();
    public List<AvailabilityRecord> FastImportRecords { get; set; } = new();
    public List<AvailabilityRecord> KnownSupplierRecords { get; set; } = new();
}
