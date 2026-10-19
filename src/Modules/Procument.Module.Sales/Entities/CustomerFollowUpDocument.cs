using Procument.Shared.Entities;

namespace Procument.Module.Sales.Entities;

/// <summary>
/// A PDF or photo attached to one Customer Follow Ups row (from the Documents dialog or the chat). The file lives at
/// Documents/CustomerFollowUps/{CustomerFollowUpId}/{FileName}; deleting the row deletes the folder.
/// </summary>
public class CustomerFollowUpDocument : BaseEntity
{
    public long CustomerFollowUpId { get; set; }
    /// <summary>Stored name on disk (a GUID), never the user's file name.</summary>
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string? MimeType { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public string? UploadedBy { get; set; }

    public CustomerFollowUp CustomerFollowUp { get; set; } = null!;
}
