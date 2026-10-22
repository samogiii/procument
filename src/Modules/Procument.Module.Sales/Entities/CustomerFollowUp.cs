using Procument.Shared.Entities;

namespace Procument.Module.Sales.Entities;

/// <summary>
/// One row of the Customer Follow Ups sheet. Customer code and Assign are free text: picking
/// from the catalog / user list is a convenience only — new values are never written back there.
/// Category and issue status are free text too; the dropdowns offer the defaults plus every
/// value already used on a row. Procurement / Sales status are fixed: Open, In Progress, Done.
/// </summary>
public class CustomerFollowUp : AuditableEntity
{
    public string? CustomerCode { get; set; }
    /// <summary>RFQ, Cert, Order or any value a user typed.</summary>
    public string? Category { get; set; }
    /// <summary>The linked RFQHeader (plain column, no FK — deleting an RFQ must not touch follow ups).</summary>
    public long? RfqId { get; set; }
    public string? PartNumber { get; set; }
    public string? RequestedItem { get; set; }
    public DateTime? DateOfRequest { get; set; }
    public DateTime? Deadline { get; set; }
    public string? Assign { get; set; }
    /// <summary>Legacy: no longer shown or edited (replaced by Procurement / Sales status); kept so old values are not lost.</summary>
    public string? ResponseStatus { get; set; }
    public string ProcurementStatus { get; set; } = "Open";
    public string SalesStatus { get; set; } = "Open";
    public string? IssueStatus { get; set; }
    public DateTime? DateOfReply { get; set; }
    public string? OurResponse { get; set; }
    public string? Feedback { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    public List<CustomerFollowUpDocument> Documents { get; set; } = [];
    public List<CustomerFollowUpMessage> Messages { get; set; } = [];
    public List<CustomerFollowUpRef> Refs { get; set; } = [];
}
