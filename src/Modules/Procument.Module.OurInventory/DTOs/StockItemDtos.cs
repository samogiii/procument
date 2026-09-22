using Microsoft.AspNetCore.Mvc;
using Procument.Shared.DTOs;

namespace Procument.Module.OurInventory.DTOs;

public sealed class StockItemQuery : PageQuery
{
    public long? PartNumberId { get; set; }
    public List<long>? WarehouseIds { get; set; }
    public List<long>? CompanyPresetIds { get; set; }
    public List<string>? Conditions { get; set; }
    /// <summary>Only lots with something left to sell.</summary>
    public bool OnlyAvailable { get; set; }
    /// <summary>Only lots below their re-order point.</summary>
    public bool LowStock { get; set; }
}

public sealed class StockItemResponse
{
    public long Id { get; set; }
    public long PartNumberId { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Condition { get; set; } = string.Empty;
    public long WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public long CompanyPresetId { get; set; }
    public string CompanyPresetName { get; set; } = string.Empty;
    public decimal QtyOnHand { get; set; }
    public decimal QtyReserved { get; set; }
    public decimal QtyAvailable { get; set; }
    /// <summary>Null for roles that may not see cost.</summary>
    public decimal? AvgUnitCost { get; set; }
    /// <summary>On-hand × average cost. Null for roles that may not see cost.</summary>
    public decimal? TotalAmount { get; set; }
    public string? CertName { get; set; }
    public DateTime? TagDate { get; set; }
    public string? BinLocation { get; set; }
    public decimal? MinQty { get; set; }
    public bool IsLowStock { get; set; }
    public string? Notes { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed record StockOption(long Id, string Name);

public sealed class StockItemFilterOptions
{
    public List<StockOption> Warehouses { get; set; } = [];
    public List<StockOption> CompanyPresets { get; set; } = [];
    public List<string> Conditions { get; set; } = [];
}

public sealed class StockReservationResponse
{
    public long Id { get; set; }
    public decimal Qty { get; set; }
    public string Status { get; set; } = string.Empty;
    public long? InvoiceItemId { get; set; }
    /// <summary>Sales Order number and customer of the line holding the stock.</summary>
    public long? InvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? CustomerName { get; set; }
    public long? QuoteItemId { get; set; }
    public long? RFQItemId { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class IncomingStockLineResponse
{
    public long POId { get; set; }
    public string PONumber { get; set; } = string.Empty;
    public string POStatus { get; set; } = string.Empty;
    public long POItemId { get; set; }
    public int PORef { get; set; }
    public long PartNumberId { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Condition { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public long? DestinationWarehouseId { get; set; }
    public string DestinationWarehouseName { get; set; } = string.Empty;
    public string CompanyPresetName { get; set; } = string.Empty;
    public int QtyOrdered { get; set; }
    public decimal QtyReceived { get; set; }
    public decimal QtyRemaining { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
}

public sealed class StockSerialResponse
{
    public long Id { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public sealed class StockItemDetailResponse
{
    public StockItemResponse Item { get; set; } = new();
    public List<StockReservationResponse> Reservations { get; set; } = [];
    public List<IncomingStockLineResponse> Incoming { get; set; } = [];
    public List<StockSerialResponse> Serials { get; set; } = [];
}

public sealed class StockMovementQuery : PageQuery
{
    public long? StockItemId { get; set; }
    public long? PartNumberId { get; set; }
    [FromQuery(Name = "type")]
    public List<string>? Types { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

public sealed class StockMovementResponse
{
    public long Id { get; set; }
    public long StockItemId { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal Qty { get; set; }
    public decimal? UnitCost { get; set; }
    public long? POId { get; set; }
    public string? PONumber { get; set; }
    public long? TrackNumberId { get; set; }
    public string? TrackNumber { get; set; }
    public long? InvoiceItemId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
