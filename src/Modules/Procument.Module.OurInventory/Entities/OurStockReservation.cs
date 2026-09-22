using Procument.Module.Identity.Entities;
using Procument.Shared.Entities;

namespace Procument.Module.OurInventory.Entities;

public class OurStockReservation : BaseEntity
{
    public long StockItemId { get; set; }
    public decimal Qty { get; set; }
    public string Status { get; set; } = OurStockReservationStatuses.Active;
    public long? RFQItemId { get; set; }
    public long? QuoteItemId { get; set; }
    public long? InvoiceItemId { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public long CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }

    public OurStockItem StockItem { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
}

public static class OurStockReservationStatuses
{
    public const string Active = "Active";
    public const string Consumed = "Consumed";
    public const string Released = "Released";
    public const string Expired = "Expired";
}
