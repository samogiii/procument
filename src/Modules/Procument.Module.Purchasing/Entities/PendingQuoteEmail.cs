using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Entities;

/// <summary>
/// The supplier email a batch of PendingSupplierQuote rows came from, kept so reviewers can read
/// the original message and its attachments next to the extracted lines.
/// </summary>
public class PendingQuoteEmail : BaseEntity
{
    public string? MessageId { get; set; }
    public string? Subject { get; set; }
    /// <summary>Full From header, e.g. "Jane Doe &lt;jane@supplier.com&gt;".</summary>
    public string? FromHeader { get; set; }
    public string? FromEmail { get; set; }
    public string? ToHeader { get; set; }
    public string? CcHeader { get; set; }
    public DateTime? EmailDate { get; set; }
    /// <summary>HTML as sent, inline images embedded as data URIs. Rendered sandboxed, never trusted.</summary>
    public string? BodyHtml { get; set; }
    public string? BodyText { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PendingQuoteEmailAttachment> Attachments { get; set; } = new List<PendingQuoteEmailAttachment>();
}

public class PendingQuoteEmailAttachment : BaseEntity
{
    public long EmailId { get; set; }
    public string FileName { get; set; } = string.Empty;
    /// <summary>Name on disk under Documents/PendingQuoteEmails/{EmailId}/.</summary>
    public string StoredFileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long SizeBytes { get; set; }

    public PendingQuoteEmail Email { get; set; } = null!;
}
