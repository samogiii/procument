using Procument.Shared.Entities;

namespace Procument.Module.Catalog.Entities;

public class Customer : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? CustomerCode { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? ContactPerson { get; set; }
    public string? ShipTo { get; set; }
    public string? BillTo { get; set; }
    public string? ShippingAccount { get; set; }
    public string? Description { get; set; }
    public int? Base { get; set; }
    public string? TermsAndConditions { get; set; }
    public string? CurrencyType { get; set; }
    public int? ExWork { get; set; }
    public string? PITermsAndConditions { get; set; }
    public string? CompanyType { get; set; }
    public string? Country { get; set; }
    public string? Emails { get; set; }
    public string? Website { get; set; }
}
