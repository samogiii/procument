using Procument.Module.Identity.Entities;
using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Entities;

public class TrackNumberDocument : BaseEntity
{
    public long TrackNumberId { get; set; }

    /// <summary>
    /// null = track-level document (e.g. airway bill).
    /// Set = part-level document for that specific POItem.
    /// </summary>
    public long? POItemId { get; set; }

    /// <summary>File name as stored on disk (may differ from original to avoid conflicts).</summary>
    public string FileName { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;
    public string? MimeType { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public long UploadedByUserId { get; set; }

    // Navigation
    public POItemTrackNumber TrackNumber { get; set; } = null!;
    public POItem? POItem { get; set; }
    public User UploadedBy { get; set; } = null!;
}
