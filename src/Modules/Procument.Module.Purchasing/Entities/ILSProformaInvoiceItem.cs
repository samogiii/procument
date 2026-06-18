using Procument.Module.Catalog.Entities;
using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Entities;

public class ILSProformaInvoiceItem : BaseEntity
{
    public long ILSProformaInvoiceId { get; set; }
    public long PartNumberId { get; set; }
    public string? AltPartNumber { get; set; }
    public string? Condition { get; set; }
    public string? CertName { get; set; }
    public double Qty { get; set; }
    public decimal SellPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string? LeadTime { get; set; }
    public string? Notes { get; set; }

    // Serial-level snapshot (from the source quote item)
    public long? ILSItemSerialId { get; set; }
    public string? SerialNumber { get; set; }
    public long? ILSItemId { get; set; }

    // Provenance
    public long? SourceQuoteId { get; set; }
    public long? SourceQuoteItemId { get; set; }

    // Navigation
    public ILSProformaInvoice ILSProformaInvoice { get; set; } = null!;
    public PartNumber PartNumber { get; set; } = null!;
}
