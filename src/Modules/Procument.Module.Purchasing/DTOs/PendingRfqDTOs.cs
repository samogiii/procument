namespace Procument.Module.Purchasing.DTOs;

/// <summary>One customer RFQ email from MailFlow's CSV Export (POST /api/pending-rfqs/import).</summary>
public class ImportPendingRfqsRequest
{
    public string? MessageId { get; set; }
    public string? Subject { get; set; }
    public string? Date { get; set; }
    /// <summary>The customer's address (for ILSmart / Rotabull mail: the real sender MailFlow found in the email).</summary>
    public string? CustomerEmail { get; set; }
    public string? CustomerName { get; set; }
    /// <summary>MailFlow mailbox (ImapAccount address) the email was read from, and its label.</summary>
    public string? Mailbox { get; set; }
    public string? MailboxLabel { get; set; }
    public List<ImportPendingRfqItem> Items { get; set; } = new();
    /// <summary>The email itself, so reviewers can read it. Optional.</summary>
    public ImportPendingQuoteEmail? Email { get; set; }
}

/// <summary>Values arrive as the extracted text; the API parses qty and deadline itself.</summary>
public class ImportPendingRfqItem
{
    public string? RfqName { get; set; }
    public string? PartNumber { get; set; }
    public string? Description { get; set; }
    public string? Qty { get; set; }
    public string? Condition { get; set; }
    public string? Priority { get; set; }
    public List<string>? Alternatives { get; set; }
    public string? Remark { get; set; }
    /// <summary>Quote deadline as MailFlow resolved it (MM/DD/YYYY).</summary>
    public string? Deadline { get; set; }
}

public class ImportPendingRfqsResponse
{
    public int Imported { get; set; }
    /// <summary>True when this email (Message-ID) was already imported — nothing was added.</summary>
    public bool Duplicate { get; set; }
    /// <summary>A duplicate whose rows had no stored email yet — the email was attached to them.</summary>
    public bool EmailAttached { get; set; }
    public long? CustomerId { get; set; }
    public string? CustomerName { get; set; }
}

public class PendingRfqItemResponse
{
    public long Id { get; set; }
    public string? MessageId { get; set; }
    public string? EmailSubject { get; set; }
    public DateTime? EmailDate { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerNameRaw { get; set; }
    public string? Mailbox { get; set; }
    public string? MailboxLabel { get; set; }
    public long? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerCode { get; set; }
    public string RfqName { get; set; } = string.Empty;
    public DateTime? Deadline { get; set; }
    public string? DeadlineRaw { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double? Qty { get; set; }
    public string? Condition { get; set; }
    public string? Priority { get; set; }
    public List<string> Alternatives { get; set; } = new();
    public string? Remark { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; }
    public long? EmailId { get; set; }
    public List<string> AttachmentNames { get; set; } = new();
    public DateTime? ReviewedAt { get; set; }
    public long? ReviewedByUserId { get; set; }
    public string? ReviewedByName { get; set; }
    public long? RfqId { get; set; }
    public string? CreatedRfqName { get; set; }
    /// <summary>
    /// Pending: the RFQ this line will land in — the RFQ name, split ABA(1), ABA(2)... in deadline order when
    /// the email's lines with that name carry different deadlines (same rule as the RFQ bulk import).
    /// </summary>
    public string? PlannedRfqName { get; set; }
    /// <summary>Pending: the deadline the RFQ gets — the line's own, or 3 days after the email arrived.</summary>
    public DateTime? PlannedDeadline { get; set; }
    /// <summary>Pending: an RFQ named PlannedRfqName already exists, so Accept would skip it until it is renamed.</summary>
    public bool RfqNameTaken { get; set; }
}

/// <summary>Edits one pending/accepted line. RFQ name and deadline apply to the line only — the panel sends them for each line of the email.</summary>
public class UpdatePendingRfqItemRequest
{
    public string RfqName { get; set; } = string.Empty;
    public DateTime? Deadline { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double? Qty { get; set; }
    public string? Condition { get; set; }
    public string? Priority { get; set; }
    public List<string>? Alternatives { get; set; }
    public string? Remark { get; set; }
}

/// <summary>RFQ name / deadline set for a whole email's pending lines at once.</summary>
public class UpdatePendingRfqEmailRequest
{
    public List<long> Ids { get; set; } = new();
    public string? RfqName { get; set; }
    public DateTime? Deadline { get; set; }
}

public class PendingRfqPageResponse
{
    public List<PendingRfqItemResponse> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalEmails { get; set; }
    public int TotalLines { get; set; }
    /// <summary>Pending: every pending line, even when a search narrows TotalLines (tab badge).</summary>
    public int PendingTotal { get; set; }
    /// <summary>Accepted/Removed: everyone who reviewed lines in the period, for the "by" filter.</summary>
    public List<QuoteReviewerOption> Reviewers { get; set; } = new();
}

/// <summary>Customer picked for a whole email's pending lines (null clears it).</summary>
public class SetPendingCustomerRequest
{
    public List<long> Ids { get; set; } = new();
    public long? CustomerId { get; set; }
}

public class SetPendingCustomerResponse
{
    public int Updated { get; set; }
    /// <summary>The sender's address that was added to the customer's contacts.</summary>
    public string? EmailAdded { get; set; }
    /// <summary>Where it went, e.g. "Contact: purchasing".</summary>
    public string? EmailTarget { get; set; }
    /// <summary>Other pending lines from the same address that got this customer too.</summary>
    public int OtherLinesLinked { get; set; }
    /// <summary>The address already belongs to this other customer, so it was not added.</summary>
    public string? EmailOnOtherCustomer { get; set; }
}

public class AcceptPendingRfqsResponse
{
    public int Accepted { get; set; }
    /// <summary>The RFQs that were created.</summary>
    public List<CreatedRfqInfo> Rfqs { get; set; } = new();
    /// <summary>Ids that could not be accepted, with the reason.</summary>
    public List<PendingQuoteFailure> Failed { get; set; } = new();
}

public record CreatedRfqInfo(long Id, string Name, string CustomerName, int ItemCount);

public record CustomerLookupItem(long Id, string Name, string? CustomerCode, string? Email);
