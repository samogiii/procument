using Procument.Shared.Entities;

namespace Procument.Module.Catalog.Entities;

public class Alternative : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Foreign keys
    public long PartNumberId { get; set; }

    // Navigation
    public PartNumber PartNumber { get; set; } = null!;
}
