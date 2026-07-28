using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Procument.Shared.Services;

public class EmailAttachment
{
    public string FileName { get; set; } = string.Empty;
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "application/pdf";
}

public class SmtpConfig
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool UseSsl { get; set; } = true;
    public string FromEmail { get; set; } = string.Empty;
    public string? FromDisplayName { get; set; }
}

/// <summary>Optional: files a copy of a sent message into the account's Sent folder via IMAP.
/// Reuses the SMTP config's Username/Password (same mailbox account).</summary>
public class ImapConfig
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 993;
    public bool UseSsl { get; set; } = true;
    public string? SentFolderName { get; set; }
}

/// <summary>Outcome of a send. The message reaching the recipient and the Sent-folder copy
/// succeed or fail independently — callers that care can surface the IMAP half to the user.</summary>
public class EmailSendResult
{
    public bool SavedToSentFolder { get; set; }
    /// <summary>Full path of the folder the copy landed in, when it succeeded.</summary>
    public string? SentFolderPath { get; set; }
    /// <summary>Why the Sent-folder copy did not happen. Null when IMAP was not configured at all.</summary>
    public string? SentFolderError { get; set; }
}

public interface IEmailService
{
    Task<EmailSendResult> SendAsync(
        SmtpConfig config,
        string toEmail,
        string? toName,
        string subject,
        string bodyHtml,
        IEnumerable<EmailAttachment>? attachments = null,
        string? ccEmail = null,
        ImapConfig? imap = null,
        CancellationToken ct = default);
}

public class SmtpEmailService : IEmailService
{
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(ILogger<SmtpEmailService> logger)
    {
        _logger = logger;
    }

    public async Task<EmailSendResult> SendAsync(
        SmtpConfig config,
        string toEmail,
        string? toName,
        string subject,
        string bodyHtml,
        IEnumerable<EmailAttachment>? attachments = null,
        string? ccEmail = null,
        ImapConfig? imap = null,
        CancellationToken ct = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(config.FromDisplayName ?? config.FromEmail, config.FromEmail));
        message.To.Add(new MailboxAddress(toName ?? toEmail, toEmail));
        if (!string.IsNullOrWhiteSpace(ccEmail))
            message.Cc.Add(MailboxAddress.Parse(ccEmail));
        message.Subject = subject;

        var builder = new BodyBuilder { HtmlBody = bodyHtml };
        if (attachments != null)
        {
            foreach (var a in attachments)
                builder.Attachments.Add(a.FileName, a.Content, ContentType.Parse(a.ContentType));
        }
        message.Body = builder.ToMessageBody();

        using var client = new SmtpClient { Timeout = 15000 };
        client.CheckCertificateRevocation = false;

        // Auto picks SslOnConnect for implicit-TLS ports (e.g. 465) and StartTls for
        // plaintext-then-upgrade ports (e.g. 587/25) — forcing StartTls unconditionally
        // breaks port 465, which expects a TLS handshake immediately on connect.
        var secureOption = config.UseSsl ? SecureSocketOptions.Auto : SecureSocketOptions.None;
        await client.ConnectAsync(config.Host, config.Port, secureOption, ct);
        if (!string.IsNullOrEmpty(config.Username))
            await client.AuthenticateAsync(config.Username, config.Password ?? string.Empty, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);

        // Plain SMTP submission does not file a copy into the account's own Sent folder
        // (that's an IMAP-client behavior, not part of the SMTP protocol). This half is
        // best-effort: the message is already delivered, so a failure here is reported
        // back to the caller and logged rather than thrown.
        var result = new EmailSendResult();
        if (imap != null)
        {
            try
            {
                result.SentFolderPath = await AppendToSentFolderAsync(imap, config, message, ct);
                result.SavedToSentFolder = true;
                _logger.LogInformation("Filed a copy of \"{Subject}\" into IMAP folder \"{Folder}\" on {Host}.",
                    subject, result.SentFolderPath, imap.Host);
            }
            catch (Exception ex)
            {
                result.SentFolderError = ex.Message;
                _logger.LogWarning(ex, "Message to {To} was sent, but the IMAP copy to the Sent folder on {Host}:{Port} failed.",
                    toEmail, imap.Host, imap.Port);
            }
        }
        return result;
    }

    /// <summary>Appends the message to the account's Sent folder. Returns the folder path used.
    /// Throws with a descriptive message when the folder cannot be resolved or written.</summary>
    private async Task<string> AppendToSentFolderAsync(ImapConfig imap, SmtpConfig config, MimeMessage message, CancellationToken ct)
    {
        using var imapClient = new ImapClient { Timeout = 30000 };
        imapClient.CheckCertificateRevocation = false;
        var secureOption = imap.UseSsl ? SecureSocketOptions.Auto : SecureSocketOptions.None;
        await imapClient.ConnectAsync(imap.Host, imap.Port, secureOption, ct);

        // ImapConfig carries no credentials of its own — the Sent folder belongs to the same
        // mailbox we just sent as, so reuse the SMTP login (falling back to the From address
        // for servers where the username is implied).
        var username = string.IsNullOrWhiteSpace(config.Username) ? config.FromEmail : config.Username;
        if (string.IsNullOrWhiteSpace(username))
            throw new InvalidOperationException("No IMAP username available: set an SMTP username or From address on the preset.");
        await imapClient.AuthenticateAsync(username, config.Password ?? string.Empty, ct);

        var sentFolder = await ResolveSentFolderAsync(imapClient, imap.SentFolderName, ct)
            ?? throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(imap.SentFolderName)
                    ? "Could not find a Sent folder on the IMAP server. Set the Sent folder name explicitly on the company preset (e.g. \"Sent\", \"INBOX.Sent\" or \"[Gmail]/Sent Mail\")."
                    : $"IMAP folder \"{imap.SentFolderName}\" does not exist on the server.");

        await sentFolder.OpenAsync(FolderAccess.ReadWrite, ct);
        await sentFolder.AppendAsync(message, MessageFlags.Seen, ct);
        var path = sentFolder.FullName;
        await imapClient.DisconnectAsync(true, ct);
        return path;
    }

    /// <summary>Finds the Sent folder: explicit config name first, then the SPECIAL-USE flag,
    /// then a recursive scan of the personal namespace by attribute and by common name.</summary>
    private static async Task<IMailFolder?> ResolveSentFolderAsync(ImapClient client, string? configuredName, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(configuredName))
        {
            try { return await client.GetFolderAsync(configuredName, ct); }
            catch (FolderNotFoundException) { /* fall through to auto-detection */ }
        }

        if (client.Capabilities.HasFlag(ImapCapabilities.SpecialUse) || client.Capabilities.HasFlag(ImapCapabilities.XList))
        {
            var special = client.GetFolder(SpecialFolder.Sent);
            if (special != null) return special;
        }

        // Servers without SPECIAL-USE need a manual scan. Sent lives one level down on some
        // providers (Gmail nests it under "[Gmail]"), so descend a couple of levels.
        var personal = client.PersonalNamespaces.FirstOrDefault();
        if (personal == null) return null;

        var folders = await CollectFoldersAsync(client.GetFolder(personal), depth: 2, ct);
        var byAttribute = folders.FirstOrDefault(f => f.Attributes.HasFlag(FolderAttributes.Sent));
        if (byAttribute != null) return byAttribute;

        var names = new[] { "Sent", "Sent Items", "Sent Messages", "Sent Mail" };
        return folders.FirstOrDefault(f => names.Contains(f.Name, StringComparer.OrdinalIgnoreCase));
    }

    private static async Task<List<IMailFolder>> CollectFoldersAsync(IMailFolder root, int depth, CancellationToken ct)
    {
        var collected = new List<IMailFolder>();
        if (depth <= 0) return collected;

        IList<IMailFolder> children;
        try { children = await root.GetSubfoldersAsync(false, ct); }
        catch { return collected; }

        foreach (var child in children)
        {
            collected.Add(child);
            if (!child.Attributes.HasFlag(FolderAttributes.NoInferiors))
                collected.AddRange(await CollectFoldersAsync(child, depth - 1, ct));
        }
        return collected;
    }
}
