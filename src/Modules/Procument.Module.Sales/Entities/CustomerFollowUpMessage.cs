using Procument.Shared.Entities;

namespace Procument.Module.Sales.Entities;

/// <summary>
/// One chat message on a Customer Follow Ups row. A message may carry a PDF: the file is saved as
/// a normal row document (so it shows in the Documents column too) and linked here. Only the
/// sender can delete a message; deleting it deletes its document as well.
/// </summary>
public class CustomerFollowUpMessage : BaseEntity
{
    public long CustomerFollowUpId { get; set; }
    public string? Text { get; set; }
    /// <summary>Null when the message has no file, or its file was deleted from the Documents dialog.</summary>
    public long? DocumentId { get; set; }
    /// <summary>The file's name when sent — still shown after the document itself is deleted.</summary>
    public string? DocumentName { get; set; }
    public string SentBy { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public CustomerFollowUp CustomerFollowUp { get; set; } = null!;
    public CustomerFollowUpDocument? Document { get; set; }
}
