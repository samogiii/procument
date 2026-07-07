using MailKit.Net.Smtp;
using MailKit.Security;
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

public interface IEmailService
{
    Task SendAsync(
        SmtpConfig config,
        string toEmail,
        string? toName,
        string subject,
        string bodyHtml,
        IEnumerable<EmailAttachment>? attachments = null,
        string? ccEmail = null,
        CancellationToken ct = default);
}

public class SmtpEmailService : IEmailService
{
    public async Task SendAsync(
        SmtpConfig config,
        string toEmail,
        string? toName,
        string subject,
        string bodyHtml,
        IEnumerable<EmailAttachment>? attachments = null,
        string? ccEmail = null,
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
    }
}
