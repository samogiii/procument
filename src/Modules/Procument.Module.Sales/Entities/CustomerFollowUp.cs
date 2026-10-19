using Procument.Shared.Entities;

namespace Procument.Module.Sales.Entities;

/// <summary>
/// One row of the Customer Follow Ups sheet. Customer code and Assign are free text: picking
/// from the catalog / user list is a convenience only — new values are never written back there.
/// Response and issue statuses are free text too; the dropdowns offer the defaults plus every
/// value already used on a row.
/// </summary>
public class CustomerFollowUp : AuditableEntity
{
    public string? CustomerCode { get; set; }
    public string? PartNumber { get; set; }
    public string? RequestedItem { get; set; }
    public DateTime? DateOfRequest { get; set; }
    public DateTime? Deadline { get; set; }
    public string? Assign { get; set; }
    public string? ResponseStatus { get; set; }
    public string? IssueStatus { get; set; }
    public DateTime? DateOfReply { get; set; }
    public string? OurResponse { get; set; }
    public string? Feedback { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    public List<CustomerFollowUpDocument> Documents { get; set; } = [];
    public List<CustomerFollowUpMessage> Messages { get; set; } = [];
}
