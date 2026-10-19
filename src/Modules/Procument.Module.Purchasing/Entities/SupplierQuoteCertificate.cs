using Procument.Module.Identity.Entities;
using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Entities;

/// <summary>A certificate PDF uploaded for one supplier-part quote.</summary>
public class SupplierQuoteCertificate : BaseEntity
{
    public long SupplierQuoteId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string? MimeType { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public long UploadedByUserId { get; set; }
    /// <summary>Set when copied from an email quote's cert PDF, so re-applying it doesn't duplicate the file.</summary>
    public long? SourcePendingCertificateId { get; set; }

    public ProcumentRecord SupplierQuote { get; set; } = null!;
    public User UploadedBy { get; set; } = null!;
}
