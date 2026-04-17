using Procument.Shared.Entities;

namespace Procument.Module.Catalog.Entities;

public class PartNumber : BaseEntity
{
    
    public string? NewName { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Remark { get; set; }
    public bool IsFavorite { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Foreign keys
    public long? SupplierId { get; set; }

    // Navigation
    public Supplier? Supplier { get; set; }
    public ICollection<Alternative> Alternatives { get; set; } = new List<Alternative>();
    public ICollection<PartNumberSupplier> PartNumberSuppliers { get; set; } = new List<PartNumberSupplier>();
}
