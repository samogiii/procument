using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Entities;

public class ILSCustomer : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? CustomerCode { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? ContactPerson { get; set; }
    public string? Address { get; set; }
    public string? Description { get; set; }
}
