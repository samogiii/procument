using Procument.Shared.Entities;

namespace Procument.Module.Catalog.Entities;

public class Customer : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? ShipTo { get; set; }
    public string? BillTo { get; set; }
}
