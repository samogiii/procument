using Microsoft.EntityFrameworkCore;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Entities;

namespace Procument.Module.Purchasing.Services;

/// <summary>
/// The source emails MailFlow sends with its lines — shared by Email Quotes (PendingSupplierQuote) and
/// Email RFQs (PendingRfqItem): one PendingQuoteEmail row per message, attachments on disk under
/// Documents/PendingQuoteEmails/{emailId}/ with random names.
/// </summary>
internal static class PendingEmailStore
{
    public const string EmailRoot = "Documents/PendingQuoteEmails";

    /// <summary>Stores the email and writes its attachments to disk.</summary>
    public static async Task<PendingQuoteEmail> SaveAsync(
        DbContext db, ImportPendingQuoteEmail e, string? messageId, string? subject, string? fromEmail, DateTime? emailDate)
    {
        var email = new PendingQuoteEmail
        {
            MessageId = messageId,
            Subject = subject,
            FromHeader = Trunc(NullIfBlank(e.From), 500),
            FromEmail = fromEmail,
            ToHeader = Trunc(NullIfBlank(e.To), 2000),
            CcHeader = Trunc(NullIfBlank(e.Cc), 2000),
            EmailDate = emailDate,
            BodyHtml = NullIfBlank(e.BodyHtml),
            BodyText = NullIfBlank(e.BodyText),
        };
        db.Set<PendingQuoteEmail>().Add(email);
        await db.SaveChangesAsync(); // Id for the folder name

        var folder = Path.Combine(EmailRoot, email.Id.ToString());
        foreach (var file in e.Attachments.Where(a => !string.IsNullOrEmpty(a.ContentBase64)))
        {
            byte[] data;
            try { data = Convert.FromBase64String(file.ContentBase64!); }
            catch (FormatException) { continue; }

            Directory.CreateDirectory(folder);
            var fileName = Trunc(Path.GetFileName(NullIfBlank(file.FileName) ?? "attachment"), 255)!;
            var stored = $"{Guid.NewGuid():N}{Path.GetExtension(fileName)}";
            await File.WriteAllBytesAsync(Path.Combine(folder, stored), data);
            email.Attachments.Add(new PendingQuoteEmailAttachment
            {
                FileName = fileName,
                StoredFileName = stored,
                ContentType = Trunc(NullIfBlank(file.ContentType), 200) ?? "application/octet-stream",
                SizeBytes = data.Length,
            });
        }
        await db.SaveChangesAsync();
        return email;
    }

    public static async Task<PendingQuoteEmailResponse?> GetAsync(DbContext db, long emailId)
    {
        var e = await db.Set<PendingQuoteEmail>().AsNoTracking()
            .Include(x => x.Attachments)
            .FirstOrDefaultAsync(x => x.Id == emailId);
        if (e == null) return null;
        return new PendingQuoteEmailResponse
        {
            Id = e.Id,
            Subject = e.Subject,
            FromHeader = e.FromHeader,
            FromEmail = e.FromEmail,
            ToHeader = e.ToHeader,
            CcHeader = e.CcHeader,
            EmailDate = e.EmailDate,
            BodyHtml = e.BodyHtml,
            BodyText = e.BodyText,
            Attachments = e.Attachments.OrderBy(a => a.Id).Select(a => new PendingQuoteEmailAttachmentResponse
            {
                Id = a.Id,
                FileName = a.FileName,
                ContentType = a.ContentType,
                SizeBytes = a.SizeBytes,
            }).ToList(),
        };
    }

    public static async Task<(Stream stream, string fileName, string contentType)?> GetAttachmentAsync(
        DbContext db, long emailId, long attachmentId)
    {
        var a = await db.Set<PendingQuoteEmailAttachment>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == attachmentId && x.EmailId == emailId);
        if (a == null) return null;
        var path = Path.Combine(EmailRoot, emailId.ToString(), a.StoredFileName);
        return File.Exists(path)
            ? (File.OpenRead(path), a.FileName, a.ContentType ?? "application/octet-stream")
            : null;
    }

    /// <summary>File names of each email's attachments, for the "N attachments" chips on a list.</summary>
    public static async Task<Dictionary<long, List<string>>> AttachmentNamesAsync(DbContext db, IEnumerable<long?> emailIds)
    {
        var ids = emailIds.OfType<long>().Distinct().ToList();
        if (ids.Count == 0) return new();
        return (await db.Set<PendingQuoteEmailAttachment>().AsNoTracking()
                .Where(a => ids.Contains(a.EmailId))
                .OrderBy(a => a.Id)
                .Select(a => new { a.EmailId, a.FileName })
                .ToListAsync())
            .GroupBy(a => a.EmailId)
            .ToDictionary(g => g.Key, g => g.Select(a => a.FileName).ToList());
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? Trunc(string? value, int max) => value == null || value.Length <= max ? value : value[..max];
}
