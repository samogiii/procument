using Procument.Module.Catalog.Entities;
using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Entities;

/// <summary>
/// One part line a supplier quoted by email, extracted by MailFlow's Quote-Export and waiting for
/// review on the Procument page. Accepting it writes a SupplierPartQuote (ProcumentRecord) onto
/// every RFQ item for that part whose RFQ deadline has not passed.
/// </summary>
public class PendingSupplierQuote : BaseEntity
{
    /// <summary>Email Message-ID — re-running Quote-Export over the same mail never imports it twice.</summary>
    public string? MessageId { get; set; }
    public string? EmailSubject { get; set; }
    public DateTime? EmailDate { get; set; }
    public string? SupplierEmail { get; set; }
    /// <summary>Supplier name as it appeared in the email (display name or address).</summary>
    public string? SupplierNameRaw { get; set; }
    /// <summary>MailFlow mailbox the email was read from (its address) — the page's mailbox filter.</summary>
    public string? Mailbox { get; set; }
    /// <summary>That mailbox's label in MailFlow, e.g. "Aviation Sales".</summary>
    public string? MailboxLabel { get; set; }

    /// <summary>Catalog supplier — auto-matched on import, editable; required to accept.</summary>
    public long? SupplierId { get; set; }

    public string PartNumber { get; set; } = string.Empty;
    /// <summary>A different part number the supplier offers instead (alternate / superseded).</summary>
    public string? Alt { get; set; }
    public decimal? Price { get; set; }
    /// <summary>Price text exactly as extracted ("$1,250.00", "USD 50").</summary>
    public string? PriceRaw { get; set; }
    public double? Qty { get; set; }
    public string? Condition { get; set; }
    public string? CertName { get; set; }
    /// <summary>Tag date as parsed from the email; null when the text could not be read as a date.</summary>
    public DateOnly? TagDate { get; set; }
    /// <summary>Tag date exactly as written in the email ("03/2024", "tagged 2023").</summary>
    public string? TagDateRaw { get; set; }
    public string? LeadTime { get; set; }
    /// <summary>Written to the supplier quote's Note.</summary>
    public string? Remark { get; set; }

    // Filled by the reviewer before accepting — copied onto every RFQ supplier quote it writes.
    public double? ShippingCost { get; set; }
    public string? ShippingPoint { get; set; }
    /// <summary>Hand-typed sell lead time; null = derived per RFQ customer from LeadTime on accept.</summary>
    public string? SellLeadTime { get; set; }
    public string? MyNotes { get; set; }
    /// <summary>User-typed cert references, comma-separated (same format as SupplierPartQuote).</summary>
    public string? CustomCertReferences { get; set; }

    // Quote details — copied onto every RFQ supplier quote on accept (same fields as SupplierPartQuote).
    public string? SerialNumber { get; set; }
    public string? TraceTo { get; set; }
    public string? TagType { get; set; }
    public string? PriceType { get; set; }
    public string? TaggedBy { get; set; }
    public string? MoQ { get; set; }
    public string? Warranty { get; set; }
    public string? CR { get; set; }

    /// <summary>Pending | Accepted | Removed</summary>
    public string Status { get; set; } = "Pending";
    /// <summary>How many RFQ items received the quote when it was accepted.</summary>
    public int AppliedCount { get; set; }
    /// <summary>
    /// Reviewer switched off auto-fill: new RFQs no longer pick this accepted quote up, even
    /// inside the AutoApplyDays window (e.g. the supplier sold the part).
    /// </summary>
    public bool AutoApplyDisabled { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public long? ReviewedByUserId { get; set; }

    /// <summary>The source email (body + attachments); null for rows imported before emails were kept.</summary>
    public long? EmailId { get; set; }

    /// <summary>
    /// Where the line came from: null = an email read by MailFlow (Email Quotes page);
    /// <see cref="SourceProcurementPlus"/> = sourced on the Procurement Plus page, which Email Quotes never lists.
    /// Accepting writes RFQ quotes and auto-fills new RFQs the same way for both.
    /// </summary>
    public string? Source { get; set; }
    /// <summary>Procurement Plus: how the supplier was asked (Email, WeChat, Search, ILS).</summary>
    public string? SourceMethod { get; set; }
    /// <summary>Procurement Plus: the user who started sourcing this line.</summary>
    public long? CreatedByUserId { get; set; }
    /// <summary>Procurement Plus: the preferred supplier (Preferreds table) this line was sourced from.</summary>
    public long? PreferredId { get; set; }
    /// <summary>Procurement Plus: the user's preferred part (Preferreds) this line prices — lines are shown under it.</summary>
    public long? PreferredPartId { get; set; }
    /// <summary>
    /// Procurement Plus: the user's own RFQ supplier quote (SupplierPartQuote, last two weeks) this line was copied from.
    /// Accept skips that quote's own RFQ item, so the original quote and its cert PDFs stay as they are.
    /// </summary>
    public long? SourceSupplierQuoteId { get; set; }

    public const string SourceProcurementPlus = "ProcurementPlus";
    /// <summary>
    /// A supplier line on the new Procurement Plus page (PpeService): a draft until it has a price and a
    /// catalog supplier, then accepted onto every RFQ for the part whose deadline is at most 14 days ago.
    /// Neither Email Quotes nor Procurement ProMax list these.
    /// </summary>
    public const string SourcePpe = "PPE";

    public Supplier? Supplier { get; set; }
    public PendingQuoteEmail? Email { get; set; }
    /// <summary>Cert PDFs uploaded while reviewing; copied to each RFQ supplier quote on accept.</summary>
    public ICollection<PendingSupplierQuoteCertificate> Certificates { get; set; } = new List<PendingSupplierQuoteCertificate>();
}

/// <summary>A cert PDF attached to a pending email quote (Documents/PendingQuoteCertificates/{quoteId}/).</summary>
public class PendingSupplierQuoteCertificate : BaseEntity
{
    public long PendingSupplierQuoteId { get; set; }
    /// <summary>Name on disk (random).</summary>
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public long UploadedByUserId { get; set; }

    public PendingSupplierQuote PendingSupplierQuote { get; set; } = null!;
}
