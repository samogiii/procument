using Procument.Module.Identity.Entities;
using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Entities;

/// <summary>A certificate PDF uploaded for one supplier-part quote.</summary>
public class SupplierQuoteCertificate : BaseEntity
{
    public long SupplierQuoteId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string? MimeType { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public long UploadedByUserId { get; set; }

    public ProcumentRecord SupplierQuote { get; set; } = null!;
    public User UploadedBy { get; set; } = null!;
}
