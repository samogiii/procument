using Procument.Shared.Entities;

namespace Procument.Module.Catalog.Entities;

public class Supplier : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Description { get; set; }
    public string? Dependency { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string Status { get; set; } = "Approved"; // "Approved", "Pending", "Rejected"
    public long? RequestedByUserId { get; set; }

    // Navigation
    public ICollection<PartNumber> PartNumbers { get; set; } = new List<PartNumber>();
    public ICollection<PartNumberSupplier> PartNumberSuppliers { get; set; } = new List<PartNumberSupplier>();
}
