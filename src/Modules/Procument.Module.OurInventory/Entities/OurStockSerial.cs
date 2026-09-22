using Procument.Shared.Entities;
using Procument.Module.Catalog.Entities;

namespace Procument.Module.OurInventory.Entities;

public class OurStockSerial : BaseEntity
{
    public long StockItemId { get; set; }
    public long PartNumberId { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string Status { get; set; } = OurStockSerialStatuses.InStock;
    public long? ReceiptMovementId { get; set; }
    public long? IssueMovementId { get; set; }

    public OurStockItem StockItem { get; set; } = null!;
    public PartNumber PartNumber { get; set; } = null!;
    public OurStockMovement? ReceiptMovement { get; set; }
    public OurStockMovement? IssueMovement { get; set; }
}

public static class OurStockSerialStatuses
{
    public const string InStock = "InStock";
    public const string Reserved = "Reserved";
    public const string Issued = "Issued";
}
