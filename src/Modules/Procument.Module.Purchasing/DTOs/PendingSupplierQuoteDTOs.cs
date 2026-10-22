namespace Procument.Module.Purchasing.DTOs;

/// <summary>One quote email from MailFlow's Quote-Export (POST /api/pending-supplier-quotes/import).</summary>
public class ImportPendingQuotesRequest
{
    public string? MessageId { get; set; }
    public string? Subject { get; set; }
    public string? Date { get; set; }
    public string? SupplierEmail { get; set; }
    public string? SupplierName { get; set; }
    /// <summary>MailFlow mailbox (ImapAccount address) the email was read from, and its label.</summary>
    public string? Mailbox { get; set; }
    public string? MailboxLabel { get; set; }
    public List<ImportPendingQuoteItem> Items { get; set; } = new();
    /// <summary>The email itself, so reviewers can read it. Optional.</summary>
    public ImportPendingQuoteEmail? Email { get; set; }
}

public class ImportPendingQuoteEmail
{
    public string? From { get; set; }
    public string? To { get; set; }
    public string? Cc { get; set; }
    public string? BodyHtml { get; set; }
    public string? BodyText { get; set; }
    public List<ImportPendingQuoteAttachment> Attachments { get; set; } = new();
}

public class ImportPendingQuoteAttachment
{
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public string? ContentBase64 { get; set; }
}

/// <summary>Values arrive as the extracted text; the API parses price/qty itself.</summary>
public class ImportPendingQuoteItem
{
    public string? PartNumber { get; set; }
    public string? Alt { get; set; }
    public string? TagDate { get; set; }
    public string? Price { get; set; }
    public string? Qty { get; set; }
    public string? Condition { get; set; }
    public string? Cert { get; set; }
    /// <summary>Buy lead time. MailFlow sends "Stock" for STOCK / stk / no lead time given.</summary>
    public string? LeadTime { get; set; }
    public string? Remark { get; set; }
    // Optional quote details (MailFlow may send them; reviewers can also fill them in).
    public string? SerialNumber { get; set; }
    public string? TraceTo { get; set; }
    public string? TagType { get; set; }
    public string? PriceType { get; set; }
    public string? TaggedBy { get; set; }
    public string? MoQ { get; set; }
    public string? Warranty { get; set; }
    public string? CR { get; set; }
}

public class ImportPendingQuotesResponse
{
    public int Imported { get; set; }
    /// <summary>True when this email (Message-ID) was already imported — nothing was added.</summary>
    public bool Duplicate { get; set; }
    /// <summary>A duplicate whose rows had no stored email yet — the email was attached to them.</summary>
    public bool EmailAttached { get; set; }
    /// <summary>Always false: removed lines are never replaced. Kept for MailFlow compatibility.</summary>
    public bool Reread { get; set; }
    public long? SupplierId { get; set; }
    public string? SupplierName { get; set; }
}

/// <summary>Whether Procument already has an email (by Message-ID) — MailFlow skips it if so.</summary>
public class KnownEmailResponse
{
    public bool Known { get; set; }
    /// <summary>The email's text/attachments are stored (else MailFlow sends them, nothing else).</summary>
    public bool HasEmail { get; set; }
    /// <summary>Always false: a removed email is never read again. Kept for MailFlow compatibility.</summary>
    public bool NeedsReread { get; set; }
}

public class PendingSupplierQuoteResponse
{
    public string? Mailbox { get; set; }
    public string? MailboxLabel { get; set; }
    public long Id { get; set; }
    public string? MessageId { get; set; }
    public string? EmailSubject { get; set; }
    public DateTime? EmailDate { get; set; }
    public string? SupplierEmail { get; set; }
    public string? SupplierNameRaw { get; set; }
    public long? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string? Alt { get; set; }
    public decimal? Price { get; set; }
    public string? PriceRaw { get; set; }
    public DateOnly? TagDate { get; set; }
    public string? TagDateRaw { get; set; }
    public double? Qty { get; set; }
    public string? Condition { get; set; }
    public string? CertName { get; set; }
    public string? LeadTime { get; set; }
    public string? Remark { get; set; }
    public double? ShippingCost { get; set; }
    public string? ShippingPoint { get; set; }
    public string? SellLeadTime { get; set; }
    public string? MyNotes { get; set; }
    public string? SerialNumber { get; set; }
    public string? TraceTo { get; set; }
    public string? TagType { get; set; }
    public string? PriceType { get; set; }
    public string? TaggedBy { get; set; }
    public string? MoQ { get; set; }
    public string? Warranty { get; set; }
    public string? CR { get; set; }
    public List<string> CustomCertReferences { get; set; } = new();
    public List<PendingQuoteCertificateResponse> Certificates { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public long? EmailId { get; set; }
    /// <summary>File names attached to the source email (inline images excluded).</summary>
    public List<string> AttachmentNames { get; set; } = new();
    /// <summary>RFQ items (deadline at most a week ago) this part would be added to on accept.</summary>
    public List<PendingQuoteRfqMatch> Matches { get; set; } = new();

    // Accepted lines only
    public DateTime? ReviewedAt { get; set; }
    public long? ReviewedByUserId { get; set; }
    public string? ReviewedByName { get; set; }
    /// <summary>RFQ items the quote was written to (on accept, plus new RFQs that picked it up later).</summary>
    public int AppliedCount { get; set; }
    /// <summary>Auto-fill onto new RFQs was switched off by a reviewer.</summary>
    public bool AutoApplyDisabled { get; set; }
    /// <summary>New RFQs created before this moment pick the quote up (unless disabled).</summary>
    public DateTime? AutoApplyUntil { get; set; }
}

public class SetAutoApplyRequest
{
    public bool Enabled { get; set; }
}

public class PendingQuoteCertificateResponse
{
    public long Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
}

public class PendingQuoteRfqMatch
{
    public long RFQId { get; set; }
    public string RFQName { get; set; } = string.Empty;
    public long RFQItemId { get; set; }
    public DateTime Deadline { get; set; }
    public string? CustomerCode { get; set; }
    public double RequestedQty { get; set; }
    /// <summary>Set when the quoted P/N is an alternative of the RFQ part (becomes the quote's Alt P/N).</summary>
    public string? MatchedAsAlt { get; set; }
    /// <summary>The supplier already has a quote with the same condition on this item — accept updates it.</summary>
    public bool UpdatesExisting { get; set; }
}

public class PendingQuoteEmailResponse
{
    public long Id { get; set; }
    public string? Subject { get; set; }
    public string? FromHeader { get; set; }
    public string? FromEmail { get; set; }
    public string? ToHeader { get; set; }
    public string? CcHeader { get; set; }
    public DateTime? EmailDate { get; set; }
    public string? BodyHtml { get; set; }
    public string? BodyText { get; set; }
    public List<PendingQuoteEmailAttachmentResponse> Attachments { get; set; } = new();
}

public class PendingQuoteEmailAttachmentResponse
{
    public long Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long SizeBytes { get; set; }
}

public class UpdatePendingSupplierQuoteRequest
{
    public long? SupplierId { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string? Alt { get; set; }
    public DateOnly? TagDate { get; set; }
    public decimal? Price { get; set; }
    public double? Qty { get; set; }
    public string? Condition { get; set; }
    public string? CertName { get; set; }
    public string? LeadTime { get; set; }
    public string? Remark { get; set; }
    public double? ShippingCost { get; set; }
    public string? ShippingPoint { get; set; }
    /// <summary>Blank = derive per RFQ customer from LeadTime on accept.</summary>
    public string? SellLeadTime { get; set; }
    public string? MyNotes { get; set; }
    public List<string>? CustomCertReferences { get; set; }
    public string? SerialNumber { get; set; }
    public string? TraceTo { get; set; }
    public string? TagType { get; set; }
    public string? PriceType { get; set; }
    public string? TaggedBy { get; set; }
    public string? MoQ { get; set; }
    public string? Warranty { get; set; }
    public string? CR { get; set; }
}

public class PendingQuoteIdsRequest
{
    public List<long> Ids { get; set; } = new();
}

/// <summary>One page of email-quote lines. Pages hold whole emails, so the totals count both.</summary>
public class PendingQuotePageResponse
{
    public List<PendingSupplierQuoteResponse> Items { get; set; } = new();
    /// <summary>The page actually returned (clamped to the last page when the list shrank).</summary>
    public int Page { get; set; }
    /// <summary>Emails per page.</summary>
    public int PageSize { get; set; }
    public int TotalEmails { get; set; }
    public int TotalLines { get; set; }
    /// <summary>Pending: every pending line, even when a search narrows TotalLines (tab badge).</summary>
    public int PendingTotal { get; set; }
    /// <summary>Accepted/Removed: everyone who reviewed lines in the period, for the "by" filter.</summary>
    public List<QuoteReviewerOption> Reviewers { get; set; } = new();
}

public record QuoteReviewerOption(long Id, string Name);

/// <summary>A MailFlow mailbox lines came in through (dropdown); Mailbox "-" = not known (older lines).</summary>
public record MailboxOption(string Mailbox, string Label, int Lines);

/// <summary>Email Quotes: All = SuperAdmin (every mailbox), else only these (set on the Users page).</summary>
public record MyMailboxesResponse(bool All, List<string> Mailboxes);

/// <summary>Supplier picked for a whole email's pending lines (null clears it).</summary>
public class SetPendingSupplierRequest
{
    public List<long> Ids { get; set; } = new();
    public long? SupplierId { get; set; }
}

public class SetPendingSupplierResponse
{
    public int Updated { get; set; }
}

public class AcceptPendingQuotesResponse
{
    public int Accepted { get; set; }
    /// <summary>Supplier quotes written to RFQ items (new + updated).</summary>
    public int QuotesWritten { get; set; }
    /// <summary>Sender addresses added to their supplier's catalog contacts (only users allowed to).</summary>
    public List<string> EmailsAdded { get; set; } = new();
    /// <summary>"address (Other supplier)": already a contact of another catalog supplier, so not added.</summary>
    public List<string> EmailsOnOtherSupplier { get; set; } = new();
    /// <summary>Ids that could not be accepted, with the reason.</summary>
    public List<PendingQuoteFailure> Failed { get; set; } = new();
}

public class ReapplyPendingQuotesResponse
{
    /// <summary>Accepted lines written to their RFQs again.</summary>
    public int Lines { get; set; }
    /// <summary>RFQ supplier quotes rewritten with the line's current values.</summary>
    public int QuotesUpdated { get; set; }
    /// <summary>RFQ items that did not have the quote yet and got it now.</summary>
    public int QuotesAdded { get; set; }
    public List<PendingQuoteFailure> Failed { get; set; } = new();
}

public class PendingQuoteFailure
{
    public long Id { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
