using Procument.Module.Catalog.Entities;
using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Entities;

public class ILSQuoteItem : BaseEntity
{
    public long ILSQuoteId { get; set; }
    public long PartNumberId { get; set; }
    public string? AltPartNumber { get; set; }
    public string? Condition { get; set; }
    public string? CertName { get; set; }
    public double Qty { get; set; }
    public decimal SellPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string? LeadTime { get; set; }
    public string? Notes { get; set; }
    public long? ILSItemId { get; set; }

    // Navigation
    public ILSQuote ILSQuote { get; set; } = null!;
    public PartNumber PartNumber { get; set; } = null!;
    public ILSItem? ILSItem { get; set; }
}
