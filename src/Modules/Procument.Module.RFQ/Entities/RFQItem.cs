using Procument.Module.Catalog.Entities;
using Procument.Shared.Entities;

namespace Procument.Module.RFQ.Entities;

public class RFQItem : BaseEntity
{
    public string? Alt { get; set; }
    public double Qty { get; set; }
    public string? Condition { get; set; }
    public string? Unit { get; set; }

    // Foreign keys
    public long RFQId { get; set; }
    public long PartNumberId { get; set; }

    // Navigation
    public RFQHeader RFQ { get; set; } = null!;
    public PartNumber PartNumber { get; set; } = null!;
}
