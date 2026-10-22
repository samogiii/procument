using Procument.Shared.Entities;

namespace Procument.Module.Identity.Entities;

/// <summary>A MailFlow mailbox (email address) a user may see on the Email Quotes page.
/// SuperAdmin sees every mailbox; everyone else only the ones listed here.</summary>
public class UserMailbox : BaseEntity
{
    public long UserId { get; set; }
    /// <summary>Lower-case address, matches PendingSupplierQuote.Mailbox.</summary>
    public string Mailbox { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
