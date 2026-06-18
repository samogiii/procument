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

    // Quote-relevant profile fields (mirror Catalog.Customer)
    public string? BillTo { get; set; }
    public string? ShipTo { get; set; }
    public string? TermsAndConditions { get; set; }
    public string? Website { get; set; }
    public string? Country { get; set; }
    public string? ShippingAccount { get; set; }
}
