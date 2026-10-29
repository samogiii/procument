using Procument.Module.Catalog.Entities;
using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Entities;

/// <summary>
/// One part line of a customer RFQ email, extracted by MailFlow's CSV Export and waiting for review on
/// the Email RFQs page. Accepting an email's lines creates a new RFQ (RFQHeader + RFQItems) for its
/// customer, owned by the user who accepted — nothing else (no supplier quotes).
/// </summary>
public class PendingRfqItem : BaseEntity
{
    /// <summary>Email Message-ID — re-running CSV Export over the same mail never imports it twice.</summary>
    public string? MessageId { get; set; }
    public string? EmailSubject { get; set; }
    public DateTime? EmailDate { get; set; }
    /// <summary>The customer's address (for ILSmart / Rotabull mail: the real sender found in the email).</summary>
    public string? CustomerEmail { get; set; }
    /// <summary>Customer name as MailFlow found it (display name, company or address).</summary>
    public string? CustomerNameRaw { get; set; }
    /// <summary>MailFlow mailbox the email was read from (its address) — the page's mailbox filter.</summary>
    public string? Mailbox { get; set; }
    /// <summary>That mailbox's label in MailFlow, e.g. "Aviation Sales".</summary>
    public string? MailboxLabel { get; set; }

    /// <summary>Catalog customer — auto-matched on import, editable; required to accept.</summary>
    public long? CustomerId { get; set; }

    /// <summary>Name the new RFQ gets (editable per email). Lines of one email with different names become separate RFQs.</summary>
    public string RfqName { get; set; } = string.Empty;
    /// <summary>Quote deadline (RFQHeader.LeadTime); null when MailFlow could not read one.</summary>
    public DateTime? Deadline { get; set; }
    /// <summary>Deadline exactly as MailFlow sent it.</summary>
    public string? DeadlineRaw { get; set; }

    public string PartNumber { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double? Qty { get; set; }
    public string? Condition { get; set; }
    public string? Priority { get; set; }
    /// <summary>Alternative part numbers, comma-separated.</summary>
    public string? Alternatives { get; set; }
    /// <summary>Written to the RFQ item's Note (and the part number's Remark).</summary>
    public string? Remark { get; set; }

    /// <summary>Pending | Accepted | Removed — removed rows stay so the email is never imported again.</summary>
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public long? ReviewedByUserId { get; set; }

    /// <summary>The RFQ Accept created for this line, and its final name (it may carry a (1)/(2) suffix).</summary>
    public long? RfqId { get; set; }
    public string? CreatedRfqName { get; set; }

    /// <summary>The source email (body + attachments), stored like Email Quotes' emails.</summary>
    public long? EmailId { get; set; }

    /// <summary>
    /// Accepted lines: the PPE (Procurement Part Expert) — the user this part is assigned to on Procurement Plus.
    /// Every accepted line with the same P/N carries the same PPE (Sam). Plain id, no FK.
    /// </summary>
    public long? PpeUserId { get; set; }
    public DateTime? PpeAssignedAt { get; set; }
    public long? PpeAssignedByUserId { get; set; }

    public Customer? Customer { get; set; }
    public PendingQuoteEmail? Email { get; set; }
}
