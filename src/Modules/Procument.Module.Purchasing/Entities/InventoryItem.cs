using Procument.Shared.Entities;
using Procument.Module.Catalog.Entities;

namespace Procument.Module.Purchasing.Entities;

public class InventoryItem : BaseEntity
{
    public long PartNumberId { get; set; }
    public string? Description { get; set; }
    public double Qty { get; set; }
    public long CompanyId { get; set; }
    public string? Condition { get; set; }
    public decimal? Price { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public PartNumber PartNumber { get; set; } = null!;
    public Supplier Company { get; set; } = null!;
}
