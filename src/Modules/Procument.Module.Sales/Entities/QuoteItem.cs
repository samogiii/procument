using Procument.Module.Catalog.Entities;
using Procument.Module.RFQ.Entities;
using Procument.Shared.Entities;

namespace Procument.Module.Sales.Entities;

public class QuoteItem : BaseEntity
{
    public string? Alt { get; set; }
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string? Condition { get; set; }
    public int? LeadTimeDays { get; set; }

    // Foreign keys
    public long QuoteId { get; set; }
    public long? RFQItemId { get; set; }
    public long? PartNumberId { get; set; }

    // Navigation
    public Quote Quote { get; set; } = null!;
    public RFQItem? RFQItem { get; set; }
    public PartNumber? PartNumber { get; set; }
    public ICollection<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();
}
