using Procument.Module.Catalog.Entities;
using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Entities;

public class ILSItem : BaseEntity
{
    public long PartNumberId { get; set; }
    public string? Description { get; set; }
    public string? AltPartNumber { get; set; }
    public decimal Price { get; set; }
    public double Qty { get; set; }
    public string? Condition { get; set; }
    public DateOnly? TagDate { get; set; }
    public string? CertName { get; set; }
    public string? LeadTime { get; set; }
    public long? ProcumentRecordId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public PartNumber PartNumber { get; set; } = null!;
    public ProcumentRecord? ProcumentRecord { get; set; }
    public List<ILSItemSerial> Serials { get; set; } = new();
}
