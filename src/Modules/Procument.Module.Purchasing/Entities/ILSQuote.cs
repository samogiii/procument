using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Entities;

public class ILSQuote : BaseEntity
{
    public string QuoteNumber { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public long ILSCustomerId { get; set; }
    public string? RfqReference { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }

    // Per-quote Bill To / Ship To override (defaults from the customer)
    public string? BillTo { get; set; }
    public string? ShipTo { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ILSCustomer ILSCustomer { get; set; } = null!;
    public ICollection<ILSQuoteItem> Items { get; set; } = new List<ILSQuoteItem>();
}
