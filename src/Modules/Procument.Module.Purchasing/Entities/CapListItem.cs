using Procument.Shared.Entities;
using Procument.Module.Catalog.Entities;

namespace Procument.Module.Purchasing.Entities;

public class CapListItem : BaseEntity
{
    public long PartNumberId { get; set; }
    public string? Description { get; set; }
    public long CompanyId { get; set; }
    public bool IsRepair { get; set; } = false;
    public long? ProcumentRecordId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public PartNumber PartNumber { get; set; } = null!;
    public Supplier Company { get; set; } = null!;
    public ProcumentRecord? ProcumentRecord { get; set; }
}
