using Procument.Shared.Entities;

namespace Procument.Module.Catalog.Entities;

public class Supplier : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }

    // Navigation
    public ICollection<PartNumber> PartNumbers { get; set; } = new List<PartNumber>();
}
