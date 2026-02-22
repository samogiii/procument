using Procument.Shared.Entities;

namespace Procument.Module.Catalog.Entities;

public class PartNumberSupplier : BaseEntity
{
    public long PartNumberId { get; set; }
    public long SupplierId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public PartNumber PartNumber { get; set; } = null!;
    public Supplier Supplier { get; set; } = null!;
}
