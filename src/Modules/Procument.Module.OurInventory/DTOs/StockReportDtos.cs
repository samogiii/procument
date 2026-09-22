namespace Procument.Module.OurInventory.DTOs;

/// <summary>Data behind a Goods Receipt Note: what arrived on one or more receipts of a Stock PO.</summary>
public sealed class StockReceiptNoteResponse
{
    public long POId { get; set; }
    public string PONumber { get; set; } = string.Empty;
    public string NoteNumber { get; set; } = string.Empty;
    public DateTime? PODate { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? SupplierPIRef { get; set; }
    public long? CompanyPresetId { get; set; }
    public string WarehouseNames { get; set; } = string.Empty;
    public DateTime ReceivedFrom { get; set; }
    public DateTime ReceivedTo { get; set; }
    public string ReceivedBy { get; set; } = string.Empty;
    public List<string> Notes { get; set; } = [];
    /// <summary>True when the note covers only some of the PO's receipts.</summary>
    public bool IsPartialSelection { get; set; }
    public List<StockReceiptNoteLine> Lines { get; set; } = [];
}

public sealed class StockReceiptNoteLine
{
    public int PORef { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Condition { get; set; } = string.Empty;
    public int QtyOrdered { get; set; }
    /// <summary>Received on the receipts this note covers.</summary>
    public decimal QtyThisNote { get; set; }
    /// <summary>Received on every receipt of the PO so far.</summary>
    public decimal QtyReceivedTotal { get; set; }
    public decimal QtyRemaining { get; set; }
    public string? CertName { get; set; }
    public string? BinLocation { get; set; }
    public string? WarehouseName { get; set; }
    public List<string> TrackNumbers { get; set; } = [];
    public List<string> Serials { get; set; } = [];
}

/// <summary>Stock grouped by owner company and warehouse, for the stock report and valuation.</summary>
public sealed class StockValuationResponse
{
    public DateTime GeneratedAt { get; set; }
    public bool IncludesCost { get; set; }
    public int LotCount { get; set; }
    public decimal TotalQtyOnHand { get; set; }
    public decimal TotalQtyReserved { get; set; }
    public decimal? TotalValue { get; set; }
    public List<StockValuationGroup> Groups { get; set; } = [];
}

public sealed class StockValuationGroup
{
    public long CompanyPresetId { get; set; }
    public string CompanyPresetName { get; set; } = string.Empty;
    public long WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public decimal QtyOnHand { get; set; }
    public decimal QtyReserved { get; set; }
    public decimal? Value { get; set; }
    public List<StockItemResponse> Items { get; set; } = [];
}
