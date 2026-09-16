using Procument.Shared.Entities;

namespace Procument.Module.Sales.Entities;

public class InvoiceItem : BaseEntity
{
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal? Discount { get; set; }
    /// <summary>PI-specific condition. Copied from the quote initially, then independently editable.</summary>
    public string? Condition { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    /// <summary>Current purchasing/shipping state for this individual PI line.</summary>
    public string Status { get; set; } = "Not Started";

    // Foreign keys
    public long InvoiceId { get; set; }
    public long? QuoteItemId { get; set; }

    // Navigation
    public Invoice Invoice { get; set; } = null!;
    public QuoteItem? QuoteItem { get; set; }
}
