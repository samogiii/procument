using Procument.Module.Catalog.Entities;
using Procument.Shared.Entities;

namespace Procument.Module.Sales.Entities;

public class FinalInvoiceItem : BaseEntity
{
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal? Discount { get; set; }
    public string? Condition { get; set; }
    public string? CertName { get; set; }
    public string? TrackNumber { get; set; }
    public string? Carrier { get; set; }

    // Foreign keys
    public long FinalInvoiceId { get; set; }
    public long? PartNumberId { get; set; }
    public long? InvoiceItemId { get; set; }  // traceability back to proforma item

    // Navigation
    public FinalInvoice FinalInvoice { get; set; } = null!;
    public PartNumber? PartNumber { get; set; }
    public InvoiceItem? InvoiceItem { get; set; }
}
