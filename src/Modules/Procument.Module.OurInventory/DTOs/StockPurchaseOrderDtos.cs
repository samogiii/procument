using Microsoft.AspNetCore.Mvc;
using Procument.Shared.DTOs;

namespace Procument.Module.OurInventory.DTOs;

public sealed class StockPurchaseOrderLineRequest
{
    public long? PartNumberId { get; set; }
    public string? PartNumber { get; set; }
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public string? Condition { get; set; }
}

public sealed class SaveStockPurchaseOrderRequest
{
    public long SupplierId { get; set; }
    public long CompanyPresetId { get; set; }
    public long DestinationWarehouseId { get; set; }
    public string? Subject { get; set; }
    public string? SupplierPIRef { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public List<StockPurchaseOrderLineRequest> Items { get; set; } = [];
}

public sealed class StockPurchaseOrderQuery : PageQuery
{
    public List<long>? SupplierIds { get; set; }
    public List<long>? CompanyPresetIds { get; set; }
    public List<long>? WarehouseIds { get; set; }
    /// <summary>Bound from <c>status</c> so DataListPage's built-in status filter works unchanged.</summary>
    [FromQuery(Name = "status")]
    public List<string>? Statuses { get; set; }
    public DateTime? CreatedFrom { get; set; }
    public DateTime? CreatedTo { get; set; }
}

public sealed class StockPurchaseOrderLineResponse
{
    public long Id { get; set; }
    public int PORef { get; set; }
    public long PartNumberId { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string Condition { get; set; } = string.Empty;
}

public sealed class StockPurchaseOrderResponse
{
    public long Id { get; set; }
    public string PONumber { get; set; } = string.Empty;
    public string Origin { get; set; } = "Stock";
    public string Status { get; set; } = "Draft";
    public string AdminApproval { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; }
    public decimal TotalAmount { get; set; }
    public long SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public long CompanyPresetId { get; set; }
    public string CompanyPresetName { get; set; } = string.Empty;
    public long DestinationWarehouseId { get; set; }
    public string DestinationWarehouseName { get; set; } = string.Empty;
    public long? PreferredWalletId { get; set; }
    public string? Subject { get; set; }
    public string? SupplierPIRef { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public string PaymentStatus { get; set; } = "NotStarted";
    /// <summary>Status of the latest Payment Request, if any.</summary>
    public string? PrStatus { get; set; }
    /// <summary>Supplier payments recorded against this PO's PRs (USD).</summary>
    public decimal PaidAmount { get; set; }
    public int QtyOrdered { get; set; }
    public decimal QtyReceived { get; set; }
    public List<StockPurchaseOrderLineResponse> Items { get; set; } = [];
}

public sealed record StockPurchaseOrderOption(long Id, string Name);

public sealed class StockPurchaseOrderFilterOptions
{
    public List<StockPurchaseOrderOption> Suppliers { get; set; } = [];
    public List<StockPurchaseOrderOption> CompanyPresets { get; set; } = [];
    public List<StockPurchaseOrderOption> Warehouses { get; set; } = [];
    public List<string> Statuses { get; set; } = [];
}

public sealed class StockPurchaseSuggestion
{
    public long PartNumberId { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
    public int Qty { get; set; }
    public decimal SuggestedUnitPrice { get; set; }
    public decimal QtyAvailable { get; set; }
    public decimal MinQty { get; set; }
}
