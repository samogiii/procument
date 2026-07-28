using Procument.Shared.Entities;

namespace Procument.Module.Catalog.Entities;

public class CompanyPreset : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Phone { get; set; }
    public string? Website { get; set; }
    public string? Email { get; set; }
    public string? TermsAndConditions { get; set; }
    public string? LogoBase64 { get; set; }
    public string? ShipToAddress { get; set; }
    public string? ShipToPhone { get; set; }
    public string? FedexAccount { get; set; }
    public string? LogoMimeType { get; set; }
    public int SortOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ModifyAt { get; set; }

    // Bank Details
    public string? BankName { get; set; }
    public string? BankAddress { get; set; }
    public string? AccountNumber { get; set; }
    public string? BeneficiaryName { get; set; }
    public string? SwiftCode { get; set; }


    // PDF Theme
    public string PrimaryColor { get; set; } = "#1a2744";
    public string AccentColor { get; set; } = "#2563eb";

    // Custom PDF template (optional HTML/CSS overrides the default generated layout)
    public string? CustomPdfHtml { get; set; }

    // SMTP Settings (for sending quote emails)
    public bool SmtpEnabled { get; set; } = false;
    public string? SmtpHost { get; set; }
    public int? SmtpPort { get; set; }
    public string? SmtpUsername { get; set; }
    public string? SmtpPasswordEncrypted { get; set; }
    public string? SmtpPasswordIv { get; set; }
    public string? SmtpFromEmail { get; set; }
    public string? SmtpFromDisplayName { get; set; }
    public bool SmtpUseSsl { get; set; } = true;

    // IMAP Settings (optional — files a copy of sent quote emails into the account's
    // "Sent" folder, since plain SMTP submission does not do this on its own)
    public bool ImapEnabled { get; set; } = false;
    public string? ImapHost { get; set; }
    public int? ImapPort { get; set; }
    public bool ImapUseSsl { get; set; } = true;
    public string? ImapSentFolder { get; set; }

    // Multiple bank accounts per preset
    public ICollection<CompanyPresetBankAccount> BankAccounts { get; set; } = new List<CompanyPresetBankAccount>();
}
