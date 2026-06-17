using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Entities;

/// <summary>
/// One physical, serialized unit belonging to an <see cref="ILSItem"/>.
/// The parent item's Qty is kept in sync with the number of these rows.
/// </summary>
public class ILSItemSerial : BaseEntity
{
    public long ILSItemId { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string? LeadTime { get; set; }

    // Cert: at least one of text / image is provided (enforced client-side).
    public string? CertText { get; set; }
    public string? CertImageFileName { get; set; }
    public string? CertImageOriginalName { get; set; }

    // Photo of the actual part.
    public string? PartImageFileName { get; set; }
    public string? PartImageOriginalName { get; set; }

    public decimal? Price { get; set; }

    /// <summary>"Shop" or "Warehouse".</summary>
    public string? Location { get; set; }

    public string? Condition { get; set; }
    public DateOnly? TagDate { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ILSItem ILSItem { get; set; } = null!;
}
