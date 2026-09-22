using Procument.Module.Identity.Entities;
using Procument.Module.Purchasing.Entities;
using Procument.Shared.Entities;

namespace Procument.Module.OurInventory.Entities;

public class OurStockMovement : BaseEntity
{
    public long StockItemId { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Qty { get; set; }
    public decimal? UnitCost { get; set; }
    public long? POId { get; set; }
    public long? POItemId { get; set; }
    public long? TrackNumberId { get; set; }
    public long? InvoiceItemId { get; set; }
    public long? QuoteItemId { get; set; }
    public long? RFQItemId { get; set; }
    public long? WarehouseTransferId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public long CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public OurStockItem StockItem { get; set; } = null!;
    public PurchaseOrder? PurchaseOrder { get; set; }
    public POItem? POItem { get; set; }
    public POItemTrackNumber? TrackNumber { get; set; }
    public WarehouseTransfer? WarehouseTransfer { get; set; }
    public User CreatedByUser { get; set; } = null!;
}

public static class OurStockMovementTypes
{
    public const string Receipt = "Receipt";
    public const string Issue = "Issue";
    public const string Adjust = "Adjust";
    public const string TransferOut = "TransferOut";
    public const string TransferIn = "TransferIn";
    public const string ReturnToSupplier = "ReturnToSupplier";
    public const string CustomerReturn = "CustomerReturn";
    public const string Opening = "Opening";
}
