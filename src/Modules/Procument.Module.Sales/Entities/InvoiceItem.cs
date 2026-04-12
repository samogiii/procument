using Procument.Shared.Entities;

namespace Procument.Module.Sales.Entities;

public class InvoiceItem : BaseEntity
{
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal? Discount { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }

    // Foreign keys
    public long InvoiceId { get; set; }
    public long? QuoteItemId { get; set; }

    // Navigation
    public Invoice Invoice { get; set; } = null!;
    public QuoteItem? QuoteItem { get; set; }
}
