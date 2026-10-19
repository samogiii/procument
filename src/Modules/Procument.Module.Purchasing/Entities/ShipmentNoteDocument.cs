using Procument.Module.Identity.Entities;
using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Entities;

/// <summary>A file attached to a shipment note — e.g. the papers the customer handed over at a PickUp.</summary>
public class ShipmentNoteDocument : BaseEntity
{
    public long ShipmentNoteId { get; set; }

    /// <summary>
    /// null = shared by the whole note (e.g. the papers uploaded when the pickup was created).
    /// Set = belongs to that one part on the note.
    /// </summary>
    public long? POItemId { get; set; }

    /// <summary>File name as stored on disk under Documents/ShipmentNotes/{ShipmentNoteId}.</summary>
    public string FileName { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;
    public string? MimeType { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public long UploadedByUserId { get; set; }

    // Navigation
    public ShipmentNote ShipmentNote { get; set; } = null!;
    public POItem? POItem { get; set; }
    public User UploadedBy { get; set; } = null!;
}
