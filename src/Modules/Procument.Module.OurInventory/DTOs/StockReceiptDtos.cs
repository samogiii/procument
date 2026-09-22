namespace Procument.Module.OurInventory.DTOs;

public sealed class ManualStockReceiptLineRequest
{
    public long POItemId { get; set; }
    public decimal Qty { get; set; }
    public string? CertName { get; set; }
    public DateTime? TagDate { get; set; }
    public string? BinLocation { get; set; }
    public List<string>? Serials { get; set; }
}

public sealed class ManualStockReceiptRequest
{
    /// <summary>Defaults to the PO's destination warehouse.</summary>
    public long? WarehouseId { get; set; }
    public string? Note { get; set; }
    public List<ManualStockReceiptLineRequest> Lines { get; set; } = [];
}

public sealed class AddStockSerialsRequest
{
    public List<string> Serials { get; set; } = [];
}

public sealed class StockReceiptMovementResponse
{
    public long Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Qty { get; set; }
    public decimal? UnitCost { get; set; }
    public long StockItemId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public long? TrackNumberId { get; set; }
    public string? TrackNumber { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<string> Serials { get; set; } = [];
}

public sealed class StockReceiptLineResponse
{
    public long POItemId { get; set; }
    public int PORef { get; set; }
    public long PartNumberId { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
    public int QtyOrdered { get; set; }
    public decimal QtyReceived { get; set; }
    public decimal QtyRemaining { get; set; }
    public long? StockItemId { get; set; }
    public List<StockReceiptMovementResponse> Movements { get; set; } = [];
}

public sealed class StockReceiptSummaryResponse
{
    public long POId { get; set; }
    public string PONumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long? DestinationWarehouseId { get; set; }
    public bool FullyReceived { get; set; }
    public List<StockReceiptLineResponse> Lines { get; set; } = [];
}
