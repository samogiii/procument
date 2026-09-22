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
    /// <summary>Catalog supplier to put on a quote row created from this record.</summary>
    public string? SupplierName { get; set; }
}

public class PartAvailabilityResponse
{
    public long PartNumberId { get; set; }
    public List<AvailabilityRecord> InventoryRecords { get; set; } = new();
    public List<AvailabilityRecord> CapListRecords { get; set; } = new();
    public List<AvailabilityRecord> ILSRecords { get; set; } = new();
    public List<AvailabilityRecord> FastImportRecords { get; set; } = new();
    public List<AvailabilityRecord> KnownSupplierRecords { get; set; } = new();
    /// <summary>Our own stock (available quantity per lot), from the Our Inventory module.</summary>
    public List<PartAvailabilitySourceRecord> OurStockRecords { get; set; } = new();
    /// <summary>Approved Stock PO lines not yet received.</summary>
    public List<PartAvailabilitySourceRecord> IncomingStockRecords { get; set; } = new();
}
