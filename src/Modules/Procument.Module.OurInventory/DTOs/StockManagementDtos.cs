namespace Procument.Module.OurInventory.DTOs;

public sealed class StockAdjustRequest
{
    /// <summary>Signed: positive adds, negative removes.</summary>
    public decimal Qty { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class StockTransferRequest
{
    public long TargetWarehouseId { get; set; }
    public decimal Qty { get; set; }
    public string? Reason { get; set; }
}

/// <summary>Lot details that do not change quantity. Certificate is part of the lot key, so it is not editable here.</summary>
public sealed class UpdateStockItemRequest
{
    public string? BinLocation { get; set; }
    public decimal? MinQty { get; set; }
    public DateTime? TagDate { get; set; }
    public string? Notes { get; set; }
}

public sealed class OpeningStockLine
{
    public long? PartNumberId { get; set; }
    public string? PartNumber { get; set; }
    public string? Description { get; set; }
    public string? Condition { get; set; }
    public long? WarehouseId { get; set; }
    public string? Warehouse { get; set; }
    public long? CompanyPresetId { get; set; }
    public string? CompanyPreset { get; set; }
    public decimal Qty { get; set; }
    public decimal UnitCost { get; set; }
    public string? CertName { get; set; }
    public DateTime? TagDate { get; set; }
    public string? BinLocation { get; set; }
    public decimal? MinQty { get; set; }
    public List<string>? Serials { get; set; }
}

public sealed class OpeningStockRequest
{
    /// <summary>Printed on every movement, e.g. "Opening stock 2026-09-30".</summary>
    public string? Reference { get; set; }
    /// <summary>When true only validates and reports errors; nothing is saved.</summary>
    public bool DryRun { get; set; }
    public List<OpeningStockLine> Lines { get; set; } = [];
}

public sealed record OpeningStockError(int Row, string Message);

public sealed class IssueReservationRequest
{
    /// <summary>Defaults to the whole reservation.</summary>
    public decimal? Qty { get; set; }
    public string? Note { get; set; }
}

public sealed class OpeningStockResult
{
    public bool Saved { get; set; }
    public int Lines { get; set; }
    public decimal Units { get; set; }
    public int LotsTouched { get; set; }
    public int NewPartNumbers { get; set; }
    public List<OpeningStockError> Errors { get; set; } = [];
}
