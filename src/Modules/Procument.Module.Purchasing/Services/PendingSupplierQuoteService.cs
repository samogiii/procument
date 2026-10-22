using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Catalog.Entities;
using Procument.Module.Catalog.Services;
using Procument.Module.Identity.Entities;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Entities;
using Procument.Module.RFQ.Entities;
using Procument.Shared.Services;

namespace Procument.Module.Purchasing.Services;

public interface IPendingSupplierQuoteService
{
    Task<ImportPendingQuotesResponse> ImportAsync(ImportPendingQuotesRequest request);
    Task<KnownEmailResponse> GetKnownAsync(string messageId);
    // allowedMailboxes: the viewer's mailboxes (Users page); null = every mailbox (SuperAdmin).
    Task<PendingQuotePageResponse> GetPendingPageAsync(string? search, int page, int pageSize, string? mailbox = null, IReadOnlyCollection<string>? allowedMailboxes = null);
    Task<PendingQuotePageResponse> GetAcceptedPageAsync(int? days, long? reviewedByUserId, string? search, int page, int pageSize, string? mailbox = null, IReadOnlyCollection<string>? allowedMailboxes = null);
    Task<List<PendingSupplierQuoteResponse>> GetAcceptedExportAsync(int? days, long? reviewedByUserId, string? search, string? mailbox = null, IReadOnlyCollection<string>? allowedMailboxes = null);
    Task<List<MailboxOption>> GetMailboxesAsync(IReadOnlyCollection<string>? allowedMailboxes = null);
    /// <summary>The ids among these that are email lines in one of the allowed mailboxes (all of them when null).</summary>
    Task<List<long>> VisibleIdsAsync(IEnumerable<long> ids, IReadOnlyCollection<string>? allowedMailboxes);
    /// <summary>True when a line of this stored email is in one of the allowed mailboxes.</summary>
    Task<bool> EmailVisibleAsync(long emailId, IReadOnlyCollection<string>? allowedMailboxes);
    Task<PendingSupplierQuoteResponse?> UpdateAsync(long id, UpdatePendingSupplierQuoteRequest request);
    Task<SetPendingSupplierResponse> SetSupplierAsync(List<long> ids, long? supplierId);
    Task<ReapplyPendingQuotesResponse> ReapplyAsync(List<long> ids, long userId);
    Task<int> RemoveAsync(List<long> ids, long userId);
    Task<int> RemoveAllPendingAsync(long userId, IReadOnlyCollection<string>? allowedMailboxes = null);
    Task<PendingQuotePageResponse> GetRemovedPageAsync(int? days, long? reviewedByUserId, string? search, int page, int pageSize, string? mailbox = null, IReadOnlyCollection<string>? allowedMailboxes = null);
    Task<int> RestoreAsync(List<long> ids);
    Task<List<SupplierLookupItem>> SearchSuppliersAsync(string? q);
    Task<PendingQuoteEmailResponse?> GetEmailAsync(long emailId);
    Task<(Stream stream, string fileName, string contentType)?> GetAttachmentAsync(long emailId, long attachmentId);
    Task<List<PendingQuoteCertificateResponse>?> UploadCertificatesAsync(long id, long userId, IEnumerable<IFormFile> files);
    Task<(Stream stream, string fileName)?> GetCertificateAsync(long id, long certificateId);
    Task<bool> DeleteCertificateAsync(long id, long certificateId);
    Task<bool> SetAutoApplyAsync(long id, bool enabled);

    // ── Procurement Plus (lines with Source = ProcurementPlus) ──
    /// <summary>These lines as the panels show them (pending ones with their RFQ matches).</summary>
    Task<List<PendingSupplierQuoteResponse>> GetLinesAsync(ICollection<long> ids);
    /// <summary>
    /// Accept; with <paramref name="allowWithoutRfq"/> a line with no open RFQ is accepted too
    /// (AppliedCount 0) so new RFQs for the part still pick it up during AutoApplyDays. With
    /// <paramref name="addSenderEmails"/> (Menu Access "emailQuotesSupplierEmails") each accepted line's sender
    /// address is also added to its supplier's catalog contacts; otherwise the supplier is only written to the RFQs.
    /// </summary>
    Task<AcceptPendingQuotesResponse> AcceptAsync(List<long> ids, long userId, bool allowWithoutRfq = false, bool addSenderEmails = false);
    /// <summary>
    /// Catalog supplier id per (normalised) address: exact address, else same company domain. With
    /// <paramref name="addDomainContacts"/> an address found by its domain is added to that supplier's contacts.
    /// </summary>
    Task<Dictionary<string, long>> MatchSuppliersByEmailAsync(IEnumerable<string?> emails, bool addDomainContacts = false);
    /// <summary>
    /// Adds the address to the supplier's catalog contacts (contact person named after its username) unless
    /// the supplier has it already. HeldBy = the other supplier's name when another supplier holds exactly
    /// this address (then it is not added).
    /// </summary>
    Task<(bool Added, string? HeldBy)> AddEmailToSupplierAsync(long supplierId, string? email);
}

public record SupplierLookupItem(long Id, string Name, string? Email);

/// <summary>
/// Supplier quotes MailFlow extracted from email, reviewed on the Procument page. Accepting a row
/// writes it as a SupplierPartQuote onto every RFQ item for that part (by name, new name or
/// alternative) whose RFQ deadline is at most DeadlineGraceDays in the past.
/// </summary>
public class PendingSupplierQuoteService : IPendingSupplierQuoteService, IRfqItemsCreatedHandler
{
    private const string Pending = "Pending";
    private const string Removed = "Removed";
    private const string Accepted = "Accepted";
    /// <summary>Quotes may still be added up to this many days after an RFQ's deadline (Sam: one week).</summary>
    public const int DeadlineGraceDays = 7;
    /// <summary>New RFQ items automatically get email quotes accepted within this many days (Sam: 7).</summary>
    public const int AutoApplyDays = 7;
    internal const string CertificateRoot = "Documents/PendingQuoteCertificates";
    private static readonly Regex NumberPattern = new(@"\d+(?:\.\d+)?", RegexOptions.Compiled);
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    private readonly DbContext _db;

    public PendingSupplierQuoteService(DbContext db)
    {
        _db = db;
    }

    // ──── Import (MailFlow) ────

    public async Task<ImportPendingQuotesResponse> ImportAsync(ImportPendingQuotesRequest request)
    {
        var messageId = Trunc(request.MessageId?.Trim(), 500);
        var subject = Trunc(request.Subject?.Trim(), 500);
        var supplierEmail = Trunc(request.SupplierEmail?.Trim(), 320);
        var emailDate = ParseDate(request.Date);

        var existing = await (!string.IsNullOrEmpty(messageId)
                ? EmailLines().Where(p => p.MessageId == messageId)
                : EmailLines().Where(p =>
                    p.SupplierEmail == supplierEmail && p.EmailSubject == subject && p.EmailDate == emailDate))
            .ToListAsync();

        // A line already seen — pending, accepted or removed — is never imported twice. Removed
        // lines stay as tombstones (Sam: a removed email must not come back), and an email whose
        // lines were all removed takes nothing new at all.
        var active = existing.Where(p => p.Status != Removed).ToList();
        var emailRemoved = existing.Count > 0 && active.Count == 0;
        var seenParts = existing.Select(p => PnKey(p.PartNumber)).ToHashSet();
        var items = emailRemoved
            ? []
            : request.Items
                .Where(i => !string.IsNullOrWhiteSpace(i.PartNumber) && !seenParts.Contains(PnKey(i.PartNumber)))
                .ToList();

        // One stored email per message: reuse it, or store it now (also fills in rows imported
        // before emails were kept).
        var emailId = existing.Select(p => p.EmailId).FirstOrDefault(id => id != null);
        var emailAttached = false;
        if (emailId == null && request.Email != null && !emailRemoved)
        {
            emailId = (await SaveEmailAsync(request, messageId, subject, supplierEmail, emailDate)).Id;
            foreach (var row in active) row.EmailId = emailId;
            emailAttached = active.Count > 0;
        }

        var mailbox = Trunc(SupplierEmails.Normalize(request.Mailbox) ?? NullIfBlank(request.Mailbox)?.ToLowerInvariant(), 320);
        var mailboxLabel = Trunc(NullIfBlank(request.MailboxLabel), 200);
        if (mailbox != null)
            foreach (var row in existing.Where(r => r.Mailbox == null))
            {
                row.Mailbox = mailbox;
                row.MailboxLabel = mailboxLabel;
            }

        if (existing.Count > 0 && items.Count == 0)
        {
            await _db.SaveChangesAsync();
            return new ImportPendingQuotesResponse { Duplicate = true, EmailAttached = emailAttached };
        }

        var supplier = await MatchSupplierAsync(supplierEmail, request.SupplierName);
        var rows = items
            .Select(i => new PendingSupplierQuote
            {
                MessageId = messageId,
                EmailSubject = subject,
                EmailDate = emailDate,
                SupplierEmail = supplierEmail,
                SupplierNameRaw = Trunc(request.SupplierName?.Trim(), 300),
                Mailbox = mailbox,
                MailboxLabel = mailboxLabel,
                SupplierId = supplier?.Id,
                PartNumber = Trunc(i.PartNumber!.Trim(), 200)!,
                PriceRaw = Trunc(i.Price?.Trim(), 100),
                Price = ParseDecimal(i.Price),
                Qty = ParseDouble(i.Qty),
                Condition = Trunc(NullIfBlank(i.Condition)?.ToUpperInvariant(), 100),
                CertName = Trunc(NullIfBlank(i.Cert), 200),
                Alt = Trunc(NullIfBlank(i.Alt), 200),
                TagDateRaw = Trunc(NullIfBlank(i.TagDate), 100),
                TagDate = ParseTagDate(i.TagDate),
                LeadTime = Trunc(LeadTimeRules.NormalizeImported(i.LeadTime), 100),
                Remark = Trunc(NullIfBlank(i.Remark), 2000),
                SerialNumber = QuoteDetailFields.Clean(i.SerialNumber, QuoteDetailFields.SerialNumberLength),
                TraceTo = QuoteDetailFields.Clean(i.TraceTo, QuoteDetailFields.TraceToLength),
                TagType = QuoteDetailFields.Clean(i.TagType, QuoteDetailFields.TagTypeLength),
                PriceType = QuoteDetailFields.Clean(i.PriceType, QuoteDetailFields.PriceTypeLength),
                TaggedBy = QuoteDetailFields.Clean(i.TaggedBy, QuoteDetailFields.TaggedByLength),
                MoQ = QuoteDetailFields.Clean(i.MoQ, QuoteDetailFields.MoQLength),
                Warranty = QuoteDetailFields.Clean(i.Warranty, QuoteDetailFields.WarrantyLength),
                CR = QuoteDetailFields.Clean(i.CR, QuoteDetailFields.CRLength),
                EmailId = emailId,
            })
            .ToList();

        _db.Set<PendingSupplierQuote>().AddRange(rows);
        await PriceProcurementPlusLinesAsync(rows);
        await _db.SaveChangesAsync();
        return new ImportPendingQuotesResponse
        {
            Imported = rows.Count,
            SupplierId = supplier?.Id,
            SupplierName = supplier?.Name,
        };
    }

    /// <summary>
    /// Procurement Plus lines being priced for the same supplier, part and condition as these new email lines
    /// (typically added from an ILS / Excel file) take the email's price — the part card then shows them in the
    /// Email Quotes colour with "quoted by email" (EmailQuoteMatch).
    /// </summary>
    private async Task PriceProcurementPlusLinesAsync(List<PendingSupplierQuote> emailRows)
    {
        if (emailRows.Count == 0) return;
        var names = emailRows.SelectMany(r => new[] { r.PartNumber, r.Alt }).OfType<string>().Select(n => n.Trim())
            .Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var lines = await _db.Set<PendingSupplierQuote>()
            .Where(p => p.Source == PendingSupplierQuote.SourceProcurementPlus && p.Status == Pending
                && (names.Contains(p.PartNumber) || (p.Alt != null && names.Contains(p.Alt))))
            .ToListAsync();
        foreach (var line in lines)
        {
            var email = emailRows.Where(e => e.Price != null).FirstOrDefault(e => EmailQuoteMatch.Matches(line, e));
            if (email != null) EmailQuoteMatch.CopyInto(line, email);
        }
    }

    /// <summary>
    /// Known = Procument has lines from this email, so MailFlow skips it. Removed lines count as
    /// known too — a removed email is never read again (NeedsReread stays false).
    /// </summary>
    public async Task<KnownEmailResponse> GetKnownAsync(string messageId)
    {
        var id = Trunc(messageId.Trim(), 500);
        var rows = _db.Set<PendingSupplierQuote>().Where(p => p.MessageId == id);
        return new KnownEmailResponse
        {
            Known = await rows.AnyAsync(),
            // A removed email never needs its text either, so MailFlow doesn't keep re-sending it.
            HasEmail = await rows.AnyAsync(p => p.EmailId != null || p.Status == Removed),
            NeedsReread = false,
        };
    }

    /// <summary>Stores the email and writes its attachments to disk (see PendingEmailStore).</summary>
    private Task<PendingQuoteEmail> SaveEmailAsync(
        ImportPendingQuotesRequest request, string? messageId, string? subject, string? supplierEmail, DateTime? emailDate) =>
        PendingEmailStore.SaveAsync(_db, request.Email!, messageId, subject, supplierEmail, emailDate);

    public Task<PendingQuoteEmailResponse?> GetEmailAsync(long emailId) => PendingEmailStore.GetAsync(_db, emailId);

    public Task<(Stream stream, string fileName, string contentType)?> GetAttachmentAsync(long emailId, long attachmentId) =>
        PendingEmailStore.GetAttachmentAsync(_db, emailId, attachmentId);

    /// <summary>Email address first (supplier email or contacts), then normalized name.</summary>
    private async Task<Supplier?> MatchSupplierAsync(string? email, string? name)
    {
        // Found by company domain: only the line's value — the address reaches the catalog when an allowed user accepts.
        var byEmail = (await FindSuppliersByEmailAsync([email])).Values.FirstOrDefault();
        if (byEmail != null) return byEmail.Supplier;
        if (string.IsNullOrWhiteSpace(name)) return null;
        var usable = _db.Set<Supplier>().AsNoTracking().Where(s => s.Dependency != "Disabled");
        var key = SupplierNameNormalizer.Key(name);
        var candidates = await usable.Select(s => new { s.Id, s.Name, s.Status }).ToListAsync();
        var match = candidates
            .Where(s => SupplierNameNormalizer.Key(s.Name) == key)
            .OrderBy(s => s.Status == "Approved" ? 0 : 1)
            .FirstOrDefault();
        return match == null ? null : new Supplier { Id = match.Id, Name = match.Name };
    }

    // ──── Supplier ↔ sender email ────

    private sealed record EmailMatch(Supplier Supplier, bool ByDomain);

    /// <summary>
    /// The catalog supplier for each address, keyed by the normalised address:
    /// 1. one holding the exact address (main Email or a contact person) — a@x.com never matches ba@x.com;
    /// 2. else (when <paramref name="byDomain"/>) one using the same company domain — y@abc.com finds the
    ///    supplier that has x@abc.com. Free-mail domains (gmail, outlook…) never match by domain.
    /// Usable suppliers only (active, not Rejected or Disabled), Approved first. SQL Contains only
    /// pre-filters; the exact address/domain check runs on the parsed addresses.
    /// </summary>
    private async Task<Dictionary<string, EmailMatch>> FindSuppliersByEmailAsync(IEnumerable<string?> emails, bool byDomain = true)
    {
        var usable = _db.Set<Supplier>().AsNoTracking()
            .Where(s => s.IsActive && s.Status != "Rejected" && s.Dependency != "Disabled");
        var result = new Dictionary<string, EmailMatch>(StringComparer.OrdinalIgnoreCase);
        foreach (var email in emails.Select(SupplierEmails.Normalize).OfType<string>().Distinct())
        {
            var exact = (await usable
                    .Where(s => (s.Email != null && s.Email.Contains(email)) || (s.Contacts != null && s.Contacts.Contains(email)))
                    .ToListAsync())
                .Where(s => SupplierEmails.AllEmails(s).Contains(email, StringComparer.OrdinalIgnoreCase))
                .OrderBy(s => s.Status == "Approved" ? 0 : 1).ThenBy(s => s.Id)
                .FirstOrDefault();
            if (exact != null)
            {
                result[email] = new EmailMatch(exact, false);
                continue;
            }

            var domain = SupplierEmails.Domain(email);
            if (!byDomain || SupplierEmails.IsGenericDomain(domain)) continue;
            var at = "@" + domain;
            var sameDomain = (await usable
                    .Where(s => (s.Email != null && s.Email.Contains(at)) || (s.Contacts != null && s.Contacts.Contains(at)))
                    .ToListAsync())
                .Where(s => SupplierEmails.HasDomain(s, domain)) // LIKE also hits "@abc.com.tr"
                .OrderBy(s => s.Status == "Approved" ? 0 : 1).ThenBy(s => s.Id)
                .FirstOrDefault();
            if (sameDomain != null) result[email] = new EmailMatch(sameDomain, true);
        }
        return result;
    }

    /// <summary>
    /// Adds the address to the supplier's catalog Contacts as a contact person named after its username
    /// (y@abc.com → "y"), unless the supplier already has it. Returns where it went, or null.
    /// </summary>
    private async Task<string?> AddSenderContactAsync(long supplierId, string email)
    {
        var supplier = await _db.Set<Supplier>().FirstOrDefaultAsync(s => s.Id == supplierId);
        if (supplier == null) return null;
        var target = SupplierEmails.AddEmail(supplier, email, email[..email.IndexOf('@')]);
        if (target != null) await _db.SaveChangesAsync();
        return target;
    }

    /// <summary>
    /// Accepted by a user allowed to (Menu Access "emailQuotesSupplierEmails" — Sam: only AMJ and GHS): the sender's
    /// address goes on the line's supplier's catalog contacts (a contact person named after the address's
    /// username) unless it is there already, and every other pending line from that address without a
    /// supplier gets the same one. When another supplier already holds the address it is not added twice.
    /// </summary>
    private async Task LinkSenderEmailAsync(long supplierId, string? senderEmail, AcceptPendingQuotesResponse result)
    {
        var email = SupplierEmails.Normalize(senderEmail);
        if (email == null) return;

        // Only an exact holder blocks it — another supplier on the same domain does not.
        var owner = (await FindSuppliersByEmailAsync([email], byDomain: false)).Values.FirstOrDefault()?.Supplier;
        if (owner != null && owner.Id != supplierId)
        {
            result.EmailsOnOtherSupplier.Add($"{email} ({owner.Name})");
            return;
        }
        if (owner == null && await AddSenderContactAsync(supplierId, email) != null)
            result.EmailsAdded.Add(email);

        await _db.Set<PendingSupplierQuote>()
            .Where(p => p.Status == Pending && p.SupplierId == null && p.SupplierEmail == email)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.SupplierId, (long?)supplierId));
    }

    /// <summary>
    /// Supplier picked for an email: only the lines' value — the sender's address is not put on the catalog
    /// supplier here (Sam: that happens only when an allowed user accepts, see LinkSenderEmailAsync).
    /// </summary>
    public async Task<SetPendingSupplierResponse> SetSupplierAsync(List<long> ids, long? supplierId)
    {
        if (supplierId != null && !await _db.Set<Supplier>().AnyAsync(s => s.Id == supplierId))
            throw new InvalidOperationException("Supplier not found.");

        var rows = await _db.Set<PendingSupplierQuote>().Where(p => ids.Contains(p.Id) && p.Status == Pending).ToListAsync();
        foreach (var row in rows) row.SupplierId = supplierId;
        await _db.SaveChangesAsync();
        return new SetPendingSupplierResponse { Updated = rows.Count };
    }

    // ──── Review list (paged by email) ────

    public const int MaxEmailsPerPage = 100;

    private sealed record EmailPage(List<long> Ids, int Page, int PageSize, int TotalEmails, int TotalLines);

    /// <summary>
    /// Pages lines by their source email, so one email is never split across pages. Only a few light
    /// columns are read to group, order and count; the heavy build (RFQ matching, certificates) then
    /// runs for the page's lines only. Pending: newest email first; Accepted/Removed: latest review first.
    /// </summary>
    private static async Task<EmailPage> PageByEmailAsync(IQueryable<PendingSupplierQuote> query, bool byReview, int page, int pageSize)
    {
        pageSize = Math.Clamp(pageSize, 1, MaxEmailsPerPage);
        var heads = await query
            .Select(p => new { p.Id, p.MessageId, p.SupplierEmail, p.EmailSubject, p.EmailDate, p.CreatedAt, p.ReviewedAt })
            .ToListAsync();
        // Same email key the panels group by.
        var emails = heads
            .GroupBy(h => h.MessageId ?? $"{h.SupplierEmail}|{h.EmailSubject}|{h.EmailDate:O}")
            .Select(g => new
            {
                Sort = g.Max(h => byReview ? h.ReviewedAt ?? h.CreatedAt : h.EmailDate ?? h.CreatedAt),
                FirstId = g.Min(h => h.Id),
                Ids = g.Select(h => h.Id),
            })
            .OrderByDescending(e => e.Sort).ThenBy(e => e.FirstId)
            .ToList();

        var lastPage = Math.Max(1, (emails.Count + pageSize - 1) / pageSize);
        page = Math.Clamp(page, 1, lastPage);
        var ids = emails.Skip((page - 1) * pageSize).Take(pageSize).SelectMany(e => e.Ids).ToList();
        return new EmailPage(ids, page, pageSize, emails.Count, heads.Count);
    }

    /// <summary>Pending lines, one page of emails; a search keeps only lines whose P/N or Alt P/N contains it, across every page.</summary>
    public async Task<PendingQuotePageResponse> GetPendingPageAsync(string? search, int page, int pageSize, string? mailbox = null, IReadOnlyCollection<string>? allowedMailboxes = null)
    {
        var all = ForMailbox(Visible(EmailLines().AsNoTracking(), allowedMailboxes).Where(p => p.Status == Pending), mailbox, p => p.Mailbox);
        var query = all;
        var q = search?.Trim();
        if (!string.IsNullOrEmpty(q))
            query = query.Where(p => p.PartNumber.Contains(q) || (p.Alt != null && p.Alt.Contains(q)));

        var paged = await PageByEmailAsync(query, false, page, pageSize);
        return new PendingQuotePageResponse
        {
            Items = await GetPendingAsync(paged.Ids),
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalEmails = paged.TotalEmails,
            TotalLines = paged.TotalLines,
            // The tab badge counts every pending line, not just the search hits.
            PendingTotal = query == all ? paged.TotalLines : await all.CountAsync(),
        };
    }

    /// <summary>Builds these pending lines with their RFQ matches (a page, or one line after an edit).</summary>
    private async Task<List<PendingSupplierQuoteResponse>> GetPendingAsync(ICollection<long> ids)
    {
        if (ids.Count == 0) return new();
        var rows = await _db.Set<PendingSupplierQuote>().AsNoTracking()
            .Include(p => p.Supplier)
            .Include(p => p.Certificates)
            .Where(p => ids.Contains(p.Id))
            .OrderByDescending(p => p.EmailDate ?? p.CreatedAt)
            .ThenBy(p => p.Id)
            .ToListAsync();
        if (rows.Count == 0) return new();

        // A sender whose address is in the catalog now (Catalog page, or picked for another email)
        // gets that supplier — saved, so accepting uses it too.
        var unresolved = rows.Where(r => r.SupplierId == null && r.SupplierEmail != null).ToList();
        if (unresolved.Count > 0)
        {
            var found = await FindSuppliersByEmailAsync(unresolved.Select(r => r.SupplierEmail));
            foreach (var sender in unresolved.GroupBy(r => SupplierEmails.Normalize(r.SupplierEmail)))
            {
                if (sender.Key == null || !found.TryGetValue(sender.Key, out var match)) continue;
                // A company-domain match is only the line's value; the address isn't added to the catalog here.
                var supplier = match.Supplier;
                var lineIds = sender.Select(r => r.Id).ToList();
                await _db.Set<PendingSupplierQuote>()
                    .Where(p => lineIds.Contains(p.Id) && p.SupplierId == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.SupplierId, (long?)supplier.Id));
                foreach (var r in sender)
                {
                    r.SupplierId = supplier.Id;
                    r.Supplier = supplier;
                }
            }
        }

        // Attachment names only — loading the emails would pull every HTML body.
        var emailIds = rows.Where(r => r.EmailId != null).Select(r => r.EmailId!.Value).Distinct().ToList();
        var attachmentNames = (await _db.Set<PendingQuoteEmailAttachment>().AsNoTracking()
                .Where(a => emailIds.Contains(a.EmailId))
                .OrderBy(a => a.Id)
                .Select(a => new { a.EmailId, a.FileName })
                .ToListAsync())
            .GroupBy(a => a.EmailId)
            .ToDictionary(g => g.Key, g => g.Select(a => a.FileName).ToList());

        var matches = await FindOpenRfqItemsAsync(rows.SelectMany(PartKeys));
        var matchedItemIds = matches.Values.SelectMany(m => m).Select(m => m.Item.Id).Distinct().ToList();
        var supplierIds = rows.Where(r => r.SupplierId != null).Select(r => r.SupplierId!.Value).Distinct().ToList();
        var existing = (await _db.Set<ProcumentRecord>().AsNoTracking()
                .Where(r => matchedItemIds.Contains(r.RFQItemId) && supplierIds.Contains(r.SupplierId) && (r.Type ?? "Procument") != "Shop")
                .Select(r => new { r.RFQItemId, r.SupplierId, r.Condition, r.Alt, r.SerialNumber })
                .ToListAsync())
            .Select(r => (r.RFQItemId, r.SupplierId, QuoteKey(r.Condition, r.Alt, r.SerialNumber)))
            .ToHashSet();

        return rows.Select(r => new PendingSupplierQuoteResponse
        {
            Id = r.Id,
            MessageId = r.MessageId,
            EmailSubject = r.EmailSubject,
            EmailDate = r.EmailDate,
            SupplierEmail = r.SupplierEmail,
            SupplierNameRaw = r.SupplierNameRaw,
            Mailbox = r.Mailbox,
            MailboxLabel = r.MailboxLabel,
            SupplierId = r.SupplierId,
            SupplierName = r.Supplier?.Name,
            PartNumber = r.PartNumber,
            Price = r.Price,
            PriceRaw = r.PriceRaw,
            Qty = r.Qty,
            Condition = r.Condition,
            CertName = r.CertName,
            Alt = r.Alt,
            TagDate = r.TagDate,
            TagDateRaw = r.TagDateRaw,
            LeadTime = r.LeadTime,
            Remark = r.Remark,
            ShippingCost = r.ShippingCost,
            ShippingPoint = r.ShippingPoint,
            SellLeadTime = r.SellLeadTime,
            MyNotes = r.MyNotes,
            SerialNumber = r.SerialNumber,
            TraceTo = r.TraceTo,
            TagType = r.TagType,
            PriceType = r.PriceType,
            TaggedBy = r.TaggedBy,
            MoQ = r.MoQ,
            Warranty = r.Warranty,
            CR = r.CR,
            CustomCertReferences = SupplierQuoteService.SplitCertReferences(r.CustomCertReferences),
            Certificates = MapCertificates(r),
            CreatedAt = r.CreatedAt,
            EmailId = r.EmailId,
            AttachmentNames = r.EmailId != null && attachmentNames.TryGetValue(r.EmailId.Value, out var names) ? names : new(),
            Matches = TargetsFor(r, matches)
                .Select(x => new PendingQuoteRfqMatch
                {
                    RFQId = x.Item.RFQId,
                    RFQName = x.Item.RFQ.Name,
                    RFQItemId = x.Item.Id,
                    Deadline = x.Item.RFQ.LeadTime,
                    CustomerCode = x.Item.RFQ.Customer?.CustomerCode,
                    RequestedQty = x.Item.Qty,
                    MatchedAsAlt = x.MatchedAsAlt,
                    UpdatesExisting = r.SupplierId != null
                        && existing.Contains((x.Item.Id, r.SupplierId.Value, QuoteKey(r.Condition ?? x.Item.Condition, AltFor(r, x), r.SerialNumber))),
                })
                .OrderBy(x => x.Deadline)
                .ToList(),
        }).ToList();
    }

    /// <summary>Accepted lines, newest first — optionally only the last N days, one reviewer and/or a search.</summary>
    public Task<PendingQuotePageResponse> GetAcceptedPageAsync(int? days, long? reviewedByUserId, string? search, int page, int pageSize, string? mailbox = null, IReadOnlyCollection<string>? allowedMailboxes = null) =>
        GetReviewedPageAsync(Accepted, days, reviewedByUserId, search, page, pageSize, mailbox, allowedMailboxes);

    /// <summary>Removed lines (Removed tab) — ReviewedAt/ReviewedBy are when and who removed them.</summary>
    public Task<PendingQuotePageResponse> GetRemovedPageAsync(int? days, long? reviewedByUserId, string? search, int page, int pageSize, string? mailbox = null, IReadOnlyCollection<string>? allowedMailboxes = null) =>
        GetReviewedPageAsync(Removed, days, reviewedByUserId, search, page, pageSize, mailbox, allowedMailboxes);

    /// <summary>Every accepted line the Accepted tab's filters select (all pages) — for the Excel export.</summary>
    public async Task<List<PendingSupplierQuoteResponse>> GetAcceptedExportAsync(int? days, long? reviewedByUserId, string? search, string? mailbox = null, IReadOnlyCollection<string>? allowedMailboxes = null)
    {
        var query = FilterReviewed(ForMailbox(Visible(InPeriod(Accepted, days), allowedMailboxes), mailbox, p => p.Mailbox), reviewedByUserId, search);
        // Same order as the tab: latest-accepted email first, its lines together.
        return (await GetReviewedAsync(query))
            .GroupBy(r => r.MessageId ?? $"{r.SupplierEmail}|{r.EmailSubject}|{r.EmailDate:O}")
            .OrderByDescending(g => g.Max(r => r.ReviewedAt ?? r.CreatedAt)).ThenBy(g => g.Min(r => r.Id))
            .SelectMany(g => g)
            .ToList();
    }

    /// <summary>Mailbox filter: null/blank = every mailbox, "-" = emails from before MailFlow sent its mailbox.</summary>
    private static IQueryable<T> ForMailbox<T>(IQueryable<T> query, string? mailbox, System.Linq.Expressions.Expression<Func<T, string?>> column)
    {
        var m = mailbox?.Trim();
        if (string.IsNullOrEmpty(m)) return query;
        var value = m == "-" ? null : m.ToLowerInvariant();
        var param = column.Parameters[0];
        var body = System.Linq.Expressions.Expression.Equal(column.Body, System.Linq.Expressions.Expression.Constant(value, typeof(string)));
        return query.Where(System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, param));
    }

    /// <summary>Lines in the viewer's mailboxes only; null = every mailbox (SuperAdmin). Unknown-mailbox lines need null.</summary>
    private static IQueryable<PendingSupplierQuote> Visible(IQueryable<PendingSupplierQuote> query, IReadOnlyCollection<string>? allowedMailboxes) =>
        allowedMailboxes == null ? query : query.Where(p => p.Mailbox != null && allowedMailboxes.Contains(p.Mailbox));

    public async Task<List<long>> VisibleIdsAsync(IEnumerable<long> ids, IReadOnlyCollection<string>? allowedMailboxes)
    {
        var list = ids.Distinct().ToList();
        if (allowedMailboxes == null || list.Count == 0) return list;
        return await Visible(EmailLines().AsNoTracking(), allowedMailboxes).Where(p => list.Contains(p.Id)).Select(p => p.Id).ToListAsync();
    }

    public async Task<bool> EmailVisibleAsync(long emailId, IReadOnlyCollection<string>? allowedMailboxes) =>
        allowedMailboxes == null || await Visible(EmailLines().AsNoTracking(), allowedMailboxes).AnyAsync(p => p.EmailId == emailId);

    /// <summary>The mailboxes email quote lines came in through, for the page's mailbox dropdown.</summary>
    public async Task<List<MailboxOption>> GetMailboxesAsync(IReadOnlyCollection<string>? allowedMailboxes = null) =>
        (await Visible(EmailLines().AsNoTracking(), allowedMailboxes)
            .GroupBy(p => new { p.Mailbox, p.MailboxLabel })
            .Select(g => new { g.Key.Mailbox, g.Key.MailboxLabel, Lines = g.Count() })
            .ToListAsync())
        .GroupBy(m => m.Mailbox ?? "-")
        .Select(g => new MailboxOption(g.Key, g.Key == "-" ? "Unknown mailbox (older emails)" : g.Select(x => x.MailboxLabel).FirstOrDefault(l => l != null) ?? g.Key, g.Sum(x => x.Lines)))
        .OrderBy(m => m.Mailbox == "-" ? 1 : 0).ThenBy(m => m.Label)
        .ToList();

    private IQueryable<PendingSupplierQuote> InPeriod(string status, int? days)
    {
        var query = EmailLines().AsNoTracking().Where(p => p.Status == status);
        if (days is > 0)
        {
            var since = DateTime.UtcNow.AddDays(-days.Value);
            query = query.Where(p => p.ReviewedAt >= since);
        }
        return query;
    }

    private static IQueryable<PendingSupplierQuote> FilterReviewed(IQueryable<PendingSupplierQuote> query, long? reviewedByUserId, string? search)
    {
        if (reviewedByUserId != null) query = query.Where(p => p.ReviewedByUserId == reviewedByUserId);
        var q = search?.Trim();
        if (!string.IsNullOrEmpty(q))
            query = query.Where(p => p.PartNumber.Contains(q)
                || (p.Alt != null && p.Alt.Contains(q))
                || (p.SupplierNameRaw != null && p.SupplierNameRaw.Contains(q))
                || (p.SupplierEmail != null && p.SupplierEmail.Contains(q))
                || (p.EmailSubject != null && p.EmailSubject.Contains(q))
                || (p.Supplier != null && p.Supplier.Name.Contains(q)));
        return query;
    }

    private async Task<PendingQuotePageResponse> GetReviewedPageAsync(
        string status, int? days, long? reviewedByUserId, string? search, int page, int pageSize, string? mailbox,
        IReadOnlyCollection<string>? allowedMailboxes)
    {
        var inPeriod = ForMailbox(Visible(InPeriod(status, days), allowedMailboxes), mailbox, p => p.Mailbox);

        // "By" filter options: everyone in the period, before the reviewer/search filters narrow it.
        var reviewerIds = await inPeriod.Where(p => p.ReviewedByUserId != null)
            .Select(p => p.ReviewedByUserId!.Value).Distinct().ToListAsync();
        var reviewers = await _db.Set<User>().AsNoTracking()
            .Where(u => reviewerIds.Contains(u.Id))
            .OrderBy(u => u.Name)
            .Select(u => new QuoteReviewerOption(u.Id, u.Name))
            .ToListAsync();

        var query = FilterReviewed(inPeriod, reviewedByUserId, search);
        var paged = await PageByEmailAsync(query, true, page, pageSize);
        return new PendingQuotePageResponse
        {
            Items = await GetReviewedAsync(paged.Ids),
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalEmails = paged.TotalEmails,
            TotalLines = paged.TotalLines,
            Reviewers = reviewers,
        };
    }

    /// <summary>Builds these accepted/removed lines (a page, or one line after an edit).</summary>
    private async Task<List<PendingSupplierQuoteResponse>> GetReviewedAsync(ICollection<long> ids)
    {
        if (ids.Count == 0) return new();
        return await GetReviewedAsync(_db.Set<PendingSupplierQuote>().AsNoTracking().Where(p => ids.Contains(p.Id)));
    }

    private async Task<List<PendingSupplierQuoteResponse>> GetReviewedAsync(IQueryable<PendingSupplierQuote> source)
    {
        var rows = await source
            .Include(p => p.Supplier)
            .Include(p => p.Certificates)
            .OrderByDescending(p => p.ReviewedAt).ThenBy(p => p.Id)
            .ToListAsync();
        if (rows.Count == 0) return new();

        var userIds = rows.Where(r => r.ReviewedByUserId != null).Select(r => r.ReviewedByUserId!.Value).Distinct().ToList();
        var userNames = await _db.Set<User>().AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name);
        var emailIds = rows.Where(r => r.EmailId != null).Select(r => r.EmailId!.Value).Distinct().ToList();
        var attachmentNames = (await _db.Set<PendingQuoteEmailAttachment>().AsNoTracking()
                .Where(a => emailIds.Contains(a.EmailId))
                .OrderBy(a => a.Id)
                .Select(a => new { a.EmailId, a.FileName })
                .ToListAsync())
            .GroupBy(a => a.EmailId)
            .ToDictionary(g => g.Key, g => g.Select(a => a.FileName).ToList());

        return rows.Select(r => new PendingSupplierQuoteResponse
        {
            Id = r.Id,
            MessageId = r.MessageId,
            EmailSubject = r.EmailSubject,
            EmailDate = r.EmailDate,
            SupplierEmail = r.SupplierEmail,
            SupplierNameRaw = r.SupplierNameRaw,
            Mailbox = r.Mailbox,
            MailboxLabel = r.MailboxLabel,
            SupplierId = r.SupplierId,
            SupplierName = r.Supplier?.Name,
            PartNumber = r.PartNumber,
            Alt = r.Alt,
            Price = r.Price,
            PriceRaw = r.PriceRaw,
            Qty = r.Qty,
            Condition = r.Condition,
            CertName = r.CertName,
            TagDate = r.TagDate,
            TagDateRaw = r.TagDateRaw,
            LeadTime = r.LeadTime,
            Remark = r.Remark,
            ShippingCost = r.ShippingCost,
            ShippingPoint = r.ShippingPoint,
            SellLeadTime = r.SellLeadTime,
            MyNotes = r.MyNotes,
            SerialNumber = r.SerialNumber,
            TraceTo = r.TraceTo,
            TagType = r.TagType,
            PriceType = r.PriceType,
            TaggedBy = r.TaggedBy,
            MoQ = r.MoQ,
            Warranty = r.Warranty,
            CR = r.CR,
            CustomCertReferences = SupplierQuoteService.SplitCertReferences(r.CustomCertReferences),
            Certificates = MapCertificates(r),
            CreatedAt = r.CreatedAt,
            EmailId = r.EmailId,
            AttachmentNames = r.EmailId != null && attachmentNames.TryGetValue(r.EmailId.Value, out var names) ? names : new(),
            ReviewedAt = r.ReviewedAt,
            ReviewedByUserId = r.ReviewedByUserId,
            ReviewedByName = r.ReviewedByUserId != null && userNames.TryGetValue(r.ReviewedByUserId.Value, out var n) ? n : null,
            AppliedCount = r.AppliedCount,
            AutoApplyDisabled = r.AutoApplyDisabled,
            AutoApplyUntil = r.ReviewedAt?.AddDays(AutoApplyDays),
        }).ToList();
    }

    public async Task<PendingSupplierQuoteResponse?> UpdateAsync(long id, UpdatePendingSupplierQuoteRequest request)
    {
        // Accepted lines stay editable: the RFQ quotes already written are separate records, so an
        // edit only changes what new RFQs pick up from now on.
        var row = await _db.Set<PendingSupplierQuote>().FirstOrDefaultAsync(p => p.Id == id && (p.Status == Pending || p.Status == Accepted));
        if (row == null) return null;
        if (string.IsNullOrWhiteSpace(request.PartNumber))
            throw new InvalidOperationException("Part number is required.");
        if (request.SupplierId != null && !await _db.Set<Supplier>().AnyAsync(s => s.Id == request.SupplierId))
            throw new InvalidOperationException("Supplier not found.");

        row.SupplierId = request.SupplierId;
        row.PartNumber = Trunc(request.PartNumber.Trim(), 200)!;
        row.Price = request.Price;
        row.Qty = request.Qty;
        row.Condition = Trunc(NullIfBlank(request.Condition)?.ToUpperInvariant(), 100);
        row.CertName = Trunc(NullIfBlank(request.CertName), 200);
        row.Alt = Trunc(NullIfBlank(request.Alt), 200);
        if (row.TagDate != request.TagDate)
        {
            row.TagDate = request.TagDate;
            row.TagDateRaw = request.TagDate?.ToString("yyyy-MM-dd");
        }
        row.LeadTime = Trunc(NullIfBlank(request.LeadTime), 100);
        row.Remark = Trunc(NullIfBlank(request.Remark), 2000);
        row.ShippingCost = request.ShippingCost;
        row.ShippingPoint = Trunc(NullIfBlank(request.ShippingPoint), 200);
        row.SellLeadTime = Trunc(NullIfBlank(request.SellLeadTime), 100);
        row.MyNotes = Trunc(NullIfBlank(request.MyNotes), 2000);
        row.SerialNumber = QuoteDetailFields.Clean(request.SerialNumber, QuoteDetailFields.SerialNumberLength);
        row.TraceTo = QuoteDetailFields.Clean(request.TraceTo, QuoteDetailFields.TraceToLength);
        row.TagType = QuoteDetailFields.Clean(request.TagType, QuoteDetailFields.TagTypeLength);
        row.PriceType = QuoteDetailFields.Clean(request.PriceType, QuoteDetailFields.PriceTypeLength);
        row.TaggedBy = QuoteDetailFields.Clean(request.TaggedBy, QuoteDetailFields.TaggedByLength);
        row.MoQ = QuoteDetailFields.Clean(request.MoQ, QuoteDetailFields.MoQLength);
        row.Warranty = QuoteDetailFields.Clean(request.Warranty, QuoteDetailFields.WarrantyLength);
        row.CR = QuoteDetailFields.Clean(request.CR, QuoteDetailFields.CRLength);
        // null = the client didn't send the list → keep what is stored.
        if (request.CustomCertReferences != null)
            row.CustomCertReferences = Trunc(SupplierQuoteService.JoinCertReferences(request.CustomCertReferences), 1000);
        await _db.SaveChangesAsync();

        var list = row.Status == Pending ? await GetPendingAsync([id]) : await GetReviewedAsync([id]);
        return list.FirstOrDefault(p => p.Id == id);
    }

    /// <summary>Switches auto-fill onto new RFQs on/off for an accepted line. False when not found.</summary>
    public async Task<bool> SetAutoApplyAsync(long id, bool enabled)
    {
        var row = await _db.Set<PendingSupplierQuote>().FirstOrDefaultAsync(p => p.Id == id && p.Status == Accepted);
        if (row == null) return false;
        row.AutoApplyDisabled = !enabled;
        await _db.SaveChangesAsync();
        return true;
    }

    // ──── Cert PDFs on a pending line ────

    private static List<PendingQuoteCertificateResponse> MapCertificates(PendingSupplierQuote row) =>
        row.Certificates.OrderBy(c => c.Id).Select(c => new PendingQuoteCertificateResponse
        {
            Id = c.Id,
            FileName = c.OriginalFileName,
            SizeBytes = c.FileSizeBytes,
        }).ToList();

    /// <summary>null when the line doesn't exist or is no longer pending.</summary>
    public async Task<List<PendingQuoteCertificateResponse>?> UploadCertificatesAsync(long id, long userId, IEnumerable<IFormFile> files)
    {
        if (!await _db.Set<PendingSupplierQuote>().AnyAsync(p => p.Id == id && (p.Status == Pending || p.Status == Accepted))) return null;

        var valid = files.Where(f => f is { Length: > 0 }).ToList();
        if (valid.Count == 0) throw new InvalidOperationException("No files were provided.");
        if (valid.Any(f => !string.Equals(Path.GetExtension(f.FileName), ".pdf", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Only PDF certificate files can be uploaded.");

        var folder = Path.Combine(CertificateRoot, id.ToString());
        Directory.CreateDirectory(folder);
        var added = new List<PendingSupplierQuoteCertificate>();
        foreach (var file in valid)
        {
            var stored = $"{Guid.NewGuid():N}.pdf";
            await using (var stream = File.Create(Path.Combine(folder, stored)))
                await file.CopyToAsync(stream);
            var cert = new PendingSupplierQuoteCertificate
            {
                PendingSupplierQuoteId = id,
                FileName = stored,
                OriginalFileName = Trunc(Path.GetFileName(file.FileName), 255)!,
                FileSizeBytes = file.Length,
                UploadedAt = DateTime.UtcNow,
                UploadedByUserId = userId,
            };
            _db.Set<PendingSupplierQuoteCertificate>().Add(cert);
            added.Add(cert);
        }
        await _db.SaveChangesAsync();
        return added.Select(c => new PendingQuoteCertificateResponse { Id = c.Id, FileName = c.OriginalFileName, SizeBytes = c.FileSizeBytes }).ToList();
    }

    public async Task<(Stream stream, string fileName)?> GetCertificateAsync(long id, long certificateId)
    {
        var cert = await _db.Set<PendingSupplierQuoteCertificate>().AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == certificateId && c.PendingSupplierQuoteId == id);
        if (cert == null) return null;
        var path = Path.Combine(CertificateRoot, id.ToString(), cert.FileName);
        return File.Exists(path) ? (File.OpenRead(path), cert.OriginalFileName) : null;
    }

    public async Task<bool> DeleteCertificateAsync(long id, long certificateId)
    {
        var cert = await _db.Set<PendingSupplierQuoteCertificate>()
            .FirstOrDefaultAsync(c => c.Id == certificateId && c.PendingSupplierQuoteId == id
                && (c.PendingSupplierQuote.Status == Pending || c.PendingSupplierQuote.Status == Accepted));
        if (cert == null) return false;
        _db.Set<PendingSupplierQuoteCertificate>().Remove(cert);
        await _db.SaveChangesAsync();
        var path = Path.Combine(CertificateRoot, id.ToString(), cert.FileName);
        if (File.Exists(path)) File.Delete(path);
        return true;
    }

    public async Task<int> RemoveAsync(List<long> ids, long userId)
    {
        var rows = await _db.Set<PendingSupplierQuote>().Where(p => ids.Contains(p.Id) && p.Status == Pending).ToListAsync();
        foreach (var row in rows)
        {
            // Kept for good: the tombstone stops MailFlow from importing this line again.
            row.Status = Removed;
            row.ReviewedAt = DateTime.UtcNow;
            row.ReviewedByUserId = userId;
        }
        await _db.SaveChangesAsync();
        return rows.Count;
    }

    /// <summary>Header "Remove all": every pending line (in the viewer's mailboxes) on every page, in one statement.</summary>
    public Task<int> RemoveAllPendingAsync(long userId, IReadOnlyCollection<string>? allowedMailboxes = null)
    {
        var now = DateTime.UtcNow;
        return Visible(EmailLines(), allowedMailboxes)
            .Where(p => p.Status == Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, Removed)
                .SetProperty(p => p.ReviewedAt, (DateTime?)now)
                .SetProperty(p => p.ReviewedByUserId, (long?)userId));
    }

    /// <summary>Removed → Pending again, so the line can be reviewed and accepted like a new one.</summary>
    public async Task<int> RestoreAsync(List<long> ids)
    {
        var rows = await _db.Set<PendingSupplierQuote>().Where(p => ids.Contains(p.Id) && p.Status == Removed).ToListAsync();
        foreach (var row in rows)
        {
            row.Status = Pending;
            row.ReviewedAt = null;
            row.ReviewedByUserId = null;
        }
        await _db.SaveChangesAsync();
        return rows.Count;
    }

    // ──── Accept ────

    public async Task<AcceptPendingQuotesResponse> AcceptAsync(List<long> ids, long userId, bool allowWithoutRfq = false, bool addSenderEmails = false)
    {
        var result = new AcceptPendingQuotesResponse();
        var rows = await _db.Set<PendingSupplierQuote>()
            .Include(p => p.Certificates)
            .Where(p => ids.Contains(p.Id) && p.Status == Pending)
            .ToListAsync();
        if (rows.Count == 0) return result;

        var matches = await FindOpenRfqItemsAsync(rows.SelectMany(PartKeys));
        var suppliers = await SuppliersOfAsync(rows);
        var sourceItemIds = await SourceItemIdsAsync(rows);

        foreach (var row in rows)
        {
            var targets = WithoutSourceItem(row, TargetsFor(row, matches), sourceItemIds);
            var reason = CannotWriteReason(row, suppliers);
            if (reason == null && targets.Count == 0 && !allowWithoutRfq)
                reason = $"No RFQ for this part whose deadline is less than {DeadlineGraceDays} days ago.";
            if (reason == null)
            {
                if (targets.Count > 0) result.QuotesWritten += (await WriteSupplierQuotesAsync(row, targets, userId)).Total;
                row.Status = "Accepted";
                row.AppliedCount = targets.Count;
                row.ReviewedAt = DateTime.UtcNow;
                row.ReviewedByUserId = userId;
                await _db.SaveChangesAsync();
                result.Accepted++;
            }
            else
                result.Failed.Add(new PendingQuoteFailure { Id = row.Id, PartNumber = row.PartNumber, Reason = reason });
        }

        // Everyone else's accept only puts the picked supplier on the RFQs (Sam).
        if (addSenderEmails)
            foreach (var sender in rows
                         .Where(r => r.Status == Accepted && r.SupplierId != null && r.SupplierEmail != null)
                         .Select(r => (SupplierId: r.SupplierId!.Value, Email: SupplierEmails.Normalize(r.SupplierEmail)))
                         .Distinct())
                await LinkSenderEmailAsync(sender.SupplierId, sender.Email, result);
        return result;
    }

    /// <summary>
    /// Accepted tab "Re-apply": writes accepted lines to their RFQs again with their current (edited)
    /// values — rewrites the supplier quotes each line wrote before, even on RFQs past the deadline,
    /// and adds the line to every open RFQ for the part that doesn't have it yet.
    /// </summary>
    public async Task<ReapplyPendingQuotesResponse> ReapplyAsync(List<long> ids, long userId)
    {
        var result = new ReapplyPendingQuotesResponse();
        var rows = await _db.Set<PendingSupplierQuote>()
            .Include(p => p.Certificates)
            .Where(p => ids.Contains(p.Id) && p.Status == Accepted)
            .OrderBy(p => p.ReviewedAt) // same order as accepting: the newest line is written last and wins
            .ToListAsync();
        if (rows.Count == 0) return result;

        var rowIds = rows.Select(r => r.Id).ToList();
        var ownedItemIds = (await _db.Set<ProcumentRecord>().AsNoTracking()
                .Where(q => q.SourcePendingQuoteId != null && rowIds.Contains(q.SourcePendingQuoteId.Value))
                .Select(q => new { LineId = q.SourcePendingQuoteId!.Value, q.RFQItemId })
                .ToListAsync())
            .ToLookup(q => q.LineId, q => q.RFQItemId);
        var ownedIds = ownedItemIds.SelectMany(g => g).Distinct().ToList();
        var ownedItems = ownedIds.Count == 0 ? new() : await _db.Set<RFQItem>().AsNoTracking()
            .Include(i => i.RFQ).ThenInclude(r => r.Customer)
            .Include(i => i.PartNumber).ThenInclude(p => p.Alternatives)
            .Where(i => ownedIds.Contains(i.Id))
            .ToListAsync();
        var owned = IndexByPartKeys(ownedItems, rows.SelectMany(PartKeys).Select(PnKey).ToHashSet());
        var matches = await FindOpenRfqItemsAsync(rows.SelectMany(PartKeys));
        var suppliers = await SuppliersOfAsync(rows);
        var sourceItemIds = await SourceItemIdsAsync(rows);

        foreach (var row in rows)
        {
            // Items this line already wrote to (still for its part) first, then the open RFQs that don't have it yet.
            var mine = ownedItemIds[row.Id].ToHashSet();
            var targets = TargetsFor(row, owned).Where(t => mine.Contains(t.Item.Id))
                .Concat(TargetsFor(row, matches))
                .DistinctBy(t => t.Item.Id)
                .ToList();
            targets = WithoutSourceItem(row, targets, sourceItemIds);
            var reason = CannotWriteReason(row, suppliers);
            if (reason == null && targets.Count == 0)
                reason = $"No RFQ for this part whose deadline is less than {DeadlineGraceDays} days ago.";
            if (reason == null)
            {
                var written = await WriteSupplierQuotesAsync(row, targets, userId);
                result.QuotesAdded += written.Added;
                result.QuotesUpdated += written.Updated;
                row.AppliedCount = Math.Max(row.AppliedCount, targets.Count);
                await _db.SaveChangesAsync();
                result.Lines++;
            }
            else
                result.Failed.Add(new PendingQuoteFailure { Id = row.Id, PartNumber = row.PartNumber, Reason = reason });
        }
        return result;
    }

    private async Task<Dictionary<long, Supplier>> SuppliersOfAsync(List<PendingSupplierQuote> rows)
    {
        var supplierIds = rows.Where(r => r.SupplierId != null).Select(r => r.SupplierId!.Value).Distinct().ToList();
        return await _db.Set<Supplier>().AsNoTracking().Where(s => supplierIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id);
    }

    /// <summary>Why the line can't be written onto RFQs (null = it can).</summary>
    private static string? CannotWriteReason(PendingSupplierQuote row, Dictionary<long, Supplier> suppliers)
    {
        if (row.SupplierId == null || !suppliers.TryGetValue(row.SupplierId.Value, out var supplier))
            return "Pick a supplier from the catalog first.";
        if (supplier.Status != "Approved") return $"Supplier {supplier.Name} is {supplier.Status}.";
        if (row.Price == null || row.Price < 0) return "Price is missing.";
        return null;
    }

    /// <summary>RFQ item of the supplier quote each Procurement Plus line was copied from (by that quote's id).</summary>
    private async Task<Dictionary<long, long>> SourceItemIdsAsync(List<PendingSupplierQuote> rows)
    {
        var sourceQuoteIds = rows.Select(r => r.SourceSupplierQuoteId).OfType<long>().Distinct().ToList();
        return sourceQuoteIds.Count == 0 ? new()
            : await _db.Set<ProcumentRecord>().AsNoTracking().Where(q => sourceQuoteIds.Contains(q.Id))
                .ToDictionaryAsync(q => q.Id, q => q.RFQItemId);
    }

    /// <summary>Lines copied from an RFQ supplier quote (Procurement Plus, under the part) never write back onto that quote's item.</summary>
    private static List<RfqItemMatch> WithoutSourceItem(PendingSupplierQuote row, List<RfqItemMatch> targets, Dictionary<long, long> sourceItemIds) =>
        row.SourceSupplierQuoteId is long sourceId && sourceItemIds.TryGetValue(sourceId, out var sourceItemId)
            ? targets.Where(t => t.Item.Id != sourceItemId).ToList()
            : targets;

    private readonly record struct WriteResult(int Added, int Updated)
    {
        public int Total => Added + Updated;
    }

    /// <summary>
    /// Adds the quote to each RFQ item, or updates the supplier quote there that this line wrote
    /// before, else the supplier's quote for the same condition, alt P/N and serial number (so lines
    /// for different serial numbers or alternates stay separate quotes). Mirrors
    /// SupplierQuoteService.SaveAsync's side effects: links the supplier to the part and moves a
    /// Rejected RFQ back to In Progress.
    /// </summary>
    private async Task<WriteResult> WriteSupplierQuotesAsync(PendingSupplierQuote row, List<RfqItemMatch> targets, long? userId)
    {
        var supplierId = row.SupplierId!.Value;
        var itemIds = targets.Select(t => t.Item.Id).ToList();
        var existingQuotes = await _db.Set<ProcumentRecord>()
            .Where(r => itemIds.Contains(r.RFQItemId) && (r.Type ?? "Procument") != "Shop"
                && (r.SupplierId == supplierId || r.SourcePendingQuoteId == row.Id))
            .ToListAsync();
        var linkedParts = (await _db.Set<PartNumberSupplier>()
                .Where(ps => ps.SupplierId == supplierId)
                .Select(ps => ps.PartNumberId)
                .ToListAsync())
            .ToHashSet();
        var rfqIds = targets.Select(t => t.Item.RFQId).Distinct().ToList();
        var rejectedRfqs = await _db.Set<RFQHeader>().Where(h => rfqIds.Contains(h.Id) && h.Status == "Rejected").ToListAsync();
        var addedAlternatives = new HashSet<(long, string)>();
        var written = new List<(ProcumentRecord Quote, RFQItem Item)>();
        var result = new WriteResult();

        foreach (var target in targets)
        {
            var item = target.Item;
            var condition = row.Condition ?? item.Condition;
            var alt = AltFor(row, target);
            var customerBase = item.RFQ.Customer?.Base;
            var key = QuoteKey(condition, alt, row.SerialNumber);
            var quote = existingQuotes.FirstOrDefault(q => q.RFQItemId == item.Id && q.SourcePendingQuoteId == row.Id)
                ?? existingQuotes.FirstOrDefault(q => q.RFQItemId == item.Id && q.SupplierId == supplierId
                    && QuoteKey(q.Condition, q.Alt, q.SerialNumber) == key);
            if (quote == null)
            {
                quote = new ProcumentRecord
                {
                    RFQItemId = item.Id,
                    SupplierId = supplierId,
                    Price = row.Price!.Value,
                    Qty = row.Qty ?? item.Qty,
                    Condition = condition,
                    Unit = item.Unit,
                    Alt = alt,
                    CertName = row.CertName,
                    TagDate = row.TagDate,
                    LeadTime = row.LeadTime,
                    // A sell lead time typed on the email quote wins; otherwise derive it per customer base.
                    SellLeadTime = row.SellLeadTime ?? LeadTimeRules.ToSellLeadTime(row.LeadTime, customerBase),
                    Note = row.Remark,
                    MyNotes = row.MyNotes,
                    ShippingCost = row.ShippingCost,
                    ShippingPoint = row.ShippingPoint,
                    CustomCertReferences = row.CustomCertReferences,
                    SerialNumber = row.SerialNumber,
                    TraceTo = row.TraceTo,
                    TagType = row.TagType,
                    PriceType = row.PriceType,
                    TaggedBy = row.TaggedBy,
                    MoQ = row.MoQ,
                    Warranty = row.Warranty,
                    CR = row.CR,
                    // The address the quote came from is the one the user talks to for this item.
                    SupplierEmail = row.SupplierEmail,
                    SourceEmail = SourceEmailOf(row),
                    SourcePendingQuoteId = row.Id,
                    Type = "Procument",
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow,
                };
                _db.Set<ProcumentRecord>().Add(quote);
                result = result with { Added = result.Added + 1 };
            }
            else
            {
                // A quote this line wrote before takes the line as it is now (a cleared field clears it);
                // another quote of the supplier only takes what the line has.
                var own = quote.SourcePendingQuoteId == row.Id;
                quote.SupplierId = supplierId;
                quote.SourcePendingQuoteId = row.Id;
                quote.Price = row.Price!.Value;
                if (row.Qty != null) quote.Qty = row.Qty.Value;
                else if (own) quote.Qty = item.Qty;
                quote.Condition = condition;
                quote.Alt = alt;
                quote.SerialNumber = Pick(own, row.SerialNumber, quote.SerialNumber);
                quote.CertName = Pick(own, row.CertName, quote.CertName);
                quote.TagDate = Pick(own, row.TagDate, quote.TagDate);
                quote.Note = Pick(own, row.Remark, quote.Note);
                quote.MyNotes = Pick(own, row.MyNotes, quote.MyNotes);
                quote.ShippingCost = Pick(own, row.ShippingCost, quote.ShippingCost);
                quote.ShippingPoint = Pick(own, row.ShippingPoint, quote.ShippingPoint);
                quote.TraceTo = Pick(own, row.TraceTo, quote.TraceTo);
                quote.TagType = Pick(own, row.TagType, quote.TagType);
                quote.PriceType = Pick(own, row.PriceType, quote.PriceType);
                quote.TaggedBy = Pick(own, row.TaggedBy, quote.TaggedBy);
                quote.MoQ = Pick(own, row.MoQ, quote.MoQ);
                quote.Warranty = Pick(own, row.Warranty, quote.Warranty);
                quote.CR = Pick(own, row.CR, quote.CR);
                quote.SupplierEmail ??= row.SupplierEmail; // keep an email the user picked
                if (SourceEmailOf(row) is string sourceEmail) quote.SourceEmail = sourceEmail;
                if (own)
                    quote.CustomCertReferences = row.CustomCertReferences;
                else if (row.CustomCertReferences != null)
                    quote.CustomCertReferences = SupplierQuoteService.JoinCertReferences(
                        SupplierQuoteService.SplitCertReferences(quote.CustomCertReferences)
                            .Concat(SupplierQuoteService.SplitCertReferences(row.CustomCertReferences)));
                if (row.LeadTime != null)
                {
                    // Keep a hand-typed sell lead time; otherwise follow the new buy lead time.
                    if (string.IsNullOrWhiteSpace(quote.SellLeadTime) || LeadTimeRules.IsAutoSellLeadTime(quote.SellLeadTime, quote.LeadTime))
                        quote.SellLeadTime = LeadTimeRules.ToSellLeadTime(row.LeadTime, customerBase);
                    quote.LeadTime = row.LeadTime;
                }
                if (row.SellLeadTime != null) quote.SellLeadTime = row.SellLeadTime;
                if (userId != null) quote.UserId = userId;
                quote.UpdatedAt = DateTime.UtcNow; // refreshes the price age
                result = result with { Updated = result.Updated + 1 };
            }
            written.Add((quote, item));

            // Like the RFQ page: an offered alternate joins the part's catalog alternatives.
            if (alt != null && PnKey(alt) != PnKey(item.PartNumber.Name)
                && !item.PartNumber.Alternatives.Any(a => PnKey(a.Name) == PnKey(alt))
                && addedAlternatives.Add((item.PartNumberId, PnKey(alt))))
                _db.Set<Alternative>().Add(new Alternative
                {
                    PartNumberId = item.PartNumberId,
                    Name = alt,
                    CreatedAt = DateTime.UtcNow,
                });

            if (linkedParts.Add(item.PartNumberId))
                _db.Set<PartNumberSupplier>().Add(new PartNumberSupplier
                {
                    PartNumberId = item.PartNumberId,
                    SupplierId = supplierId,
                    CreatedAt = DateTime.UtcNow,
                });
        }

        foreach (var rfq in rejectedRfqs) rfq.Status = "In Progress";
        if (row.Certificates.Count > 0) await CopyCertificatesAsync(row, written);
        return result;
    }

    /// <summary>
    /// The P/N the supplier quotes for this RFQ item when it isn't the item's own (null = same P/N):
    /// the offered alternate, else the alternative of the item the email's part number matched.
    /// </summary>
    private static string? AltFor(PendingSupplierQuote row, RfqItemMatch target)
    {
        var own = new[] { target.Item.PartNumber.Name, target.Item.PartNumber.NewName }
            .Where(n => !string.IsNullOrWhiteSpace(n)).Select(PnKey).ToHashSet();
        if (row.Alt != null && !own.Contains(PnKey(row.Alt))) return row.Alt;
        if (target.MatchedAsAlt != null) return target.MatchedAsAlt;
        return own.Contains(PnKey(row.PartNumber)) ? null : row.PartNumber.Trim();
    }

    /// <summary>One supplier's quotes on an RFQ item are told apart by condition, alt P/N and serial number.</summary>
    private static string QuoteKey(string? condition, string? alt, string? serialNumber) =>
        $"{CondKey(condition)}|{PnKey(alt)}|{PnKey(serialNumber)}";

    /// <summary>The line's value; keeps the current one when the line has none, unless <paramref name="replace"/>.</summary>
    private static T Pick<T>(bool replace, T value, T current) => replace || value != null ? value : current;

    /// <summary>Sender address of a MailFlow email line; Procurement Plus lines did not come from an email.</summary>
    private static string? SourceEmailOf(PendingSupplierQuote row) =>
        row.Source == null ? row.SupplierEmail : null;

    /// <summary>
    /// Copies the email quote's cert PDFs onto each written supplier quote, named and referenced
    /// like an upload on the RFQ page. A PDF already copied to a quote (same source) is skipped,
    /// so a re-apply — or an RFQ picking the quote up later — never duplicates it.
    /// </summary>
    private async Task CopyCertificatesAsync(PendingSupplierQuote row, List<(ProcumentRecord Quote, RFQItem Item)> written)
    {
        await _db.SaveChangesAsync(); // new supplier quotes need their Id for the folder
        var quoteIds = written.Select(w => w.Quote.Id).ToList();
        var sourceIds = row.Certificates.Select(c => c.Id).ToList();
        var alreadyCopied = (await _db.Set<SupplierQuoteCertificate>().AsNoTracking()
                .Where(c => quoteIds.Contains(c.SupplierQuoteId) && c.SourcePendingCertificateId != null
                    && sourceIds.Contains(c.SourcePendingCertificateId.Value))
                .Select(c => new { c.SupplierQuoteId, c.SourcePendingCertificateId })
                .ToListAsync())
            .Select(c => (c.SupplierQuoteId, c.SourcePendingCertificateId!.Value))
            .ToHashSet();
        var itemIds = written.Select(w => w.Item.Id).Distinct().ToList();
        var certCountByItem = await _db.Set<SupplierQuoteCertificate>().AsNoTracking()
            .Where(c => itemIds.Contains(c.SupplierQuote.RFQItemId))
            .GroupBy(c => c.SupplierQuote.RFQItemId)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
        var pendingReferences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (quote, item) in written)
        {
            var safePartNumber = SupplierQuoteService.MakeSafeCertificateName(item.PartNumber.Name);
            var altSuffix = string.IsNullOrWhiteSpace(quote.Alt) ? "" : "-ALT";
            var folder = Path.Combine(SupplierQuoteService.CertificateRoot, quote.Id.ToString());
            foreach (var cert in row.Certificates.OrderBy(c => c.Id))
            {
                if (alreadyCopied.Contains((quote.Id, cert.Id))) continue;
                var source = Path.Combine(CertificateRoot, row.Id.ToString(), cert.FileName);
                if (!File.Exists(source)) continue;

                Directory.CreateDirectory(folder);
                var stored = $"{Guid.NewGuid():N}.pdf";
                File.Copy(source, Path.Combine(folder, stored));
                var count = certCountByItem.GetValueOrDefault(item.Id) + 1;
                certCountByItem[item.Id] = count;
                var reference = await SupplierQuoteService.GenerateUniqueReferenceAsync(_db, pendingReferences);
                _db.Set<SupplierQuoteCertificate>().Add(new SupplierQuoteCertificate
                {
                    SupplierQuoteId = quote.Id,
                    Reference = reference,
                    FileName = stored,
                    OriginalFileName = $"REF#{reference}-{safePartNumber}{altSuffix}-cert-{count}.pdf",
                    MimeType = "application/pdf",
                    FileSizeBytes = cert.FileSizeBytes,
                    UploadedAt = DateTime.UtcNow,
                    UploadedByUserId = cert.UploadedByUserId,
                    SourcePendingCertificateId = cert.Id,
                });
            }
        }
    }

    // ──── Matching ────

    private sealed record RfqItemMatch(RFQItem Item, string? MatchedAsAlt);

    /// <summary>The requested part number, plus the supplier's offered alternate.</summary>
    private static IEnumerable<string> PartKeys(PendingSupplierQuote row) =>
        row.Alt == null ? new[] { row.PartNumber } : new[] { row.PartNumber, row.Alt };

    /// <summary>RFQ items for the requested part number; when none, those for the offered alternate.</summary>
    private static List<RfqItemMatch> TargetsFor(PendingSupplierQuote row, Dictionary<string, List<RfqItemMatch>> matches)
    {
        if (matches.TryGetValue(PnKey(row.PartNumber), out var byPart) && byPart.Count > 0) return byPart;
        return row.Alt != null && matches.TryGetValue(PnKey(row.Alt), out var byAlt) ? byAlt : new();
    }

    /// <summary>
    /// RFQ items whose RFQ deadline is no more than DeadlineGraceDays before today, keyed by every part number they answer to
    /// (name, new name, RFQ-item alt, catalog alternatives). Keys ignore case and whitespace.
    /// </summary>
    private async Task<Dictionary<string, List<RfqItemMatch>>> FindOpenRfqItemsAsync(IEnumerable<string> partNumbers)
    {
        var wanted = partNumbers.Select(PnKey).Where(k => k.Length > 0).ToHashSet();
        var names = partNumbers.Select(p => p.Trim())
            .Concat(partNumbers.Select(p => Whitespace.Replace(p, "")))
            .Where(p => p.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var result = new Dictionary<string, List<RfqItemMatch>>();
        if (names.Count == 0) return result;

        var earliestDeadline = DateTime.UtcNow.Date.AddDays(-DeadlineGraceDays);
        var items = await _db.Set<RFQItem>().AsNoTracking()
            .Include(i => i.RFQ).ThenInclude(r => r.Customer)
            .Include(i => i.PartNumber).ThenInclude(p => p.Alternatives)
            .Where(i => i.RFQ.LeadTime >= earliestDeadline)
            .Where(i => names.Contains(i.PartNumber.Name)
                        || (i.PartNumber.NewName != null && names.Contains(i.PartNumber.NewName))
                        || (i.Alt != null && names.Contains(i.Alt))
                        || i.PartNumber.Alternatives.Any(a => names.Contains(a.Name)))
            .ToListAsync();

        return IndexByPartKeys(items, wanted);
    }

    /// <summary>
    /// Keys each RFQ item by every part number it answers to (name, new name, RFQ-item alt,
    /// catalog alternatives), keeping only keys in <paramref name="wanted"/>.
    /// Items must include RFQ.Customer and PartNumber.Alternatives.
    /// </summary>
    private static Dictionary<string, List<RfqItemMatch>> IndexByPartKeys(IEnumerable<RFQItem> items, HashSet<string> wanted)
    {
        var result = new Dictionary<string, List<RfqItemMatch>>();
        foreach (var item in items)
        {
            var own = new[] { item.PartNumber.Name, item.PartNumber.NewName }.Select(PnKey).ToHashSet();
            var alts = item.PartNumber.Alternatives.Select(a => a.Name).Append(item.Alt ?? "");
            foreach (var key in own.Where(wanted.Contains))
                Add(key, new RfqItemMatch(item, null));
            foreach (var alt in alts)
            {
                var key = PnKey(alt);
                if (wanted.Contains(key) && !own.Contains(key)) Add(key, new RfqItemMatch(item, alt.Trim()));
            }
        }
        return result;

        void Add(string key, RfqItemMatch match)
        {
            if (!result.TryGetValue(key, out var list)) result[key] = list = new();
            if (list.All(m => m.Item.Id != match.Item.Id)) list.Add(match);
        }
    }

    // ──── New RFQ items: apply recently accepted email quotes ────

    /// <summary>
    /// A part on a new RFQ gets every email quote for it (requested P/N or offered alternate)
    /// accepted in the last AutoApplyDays days — the same write as Accept, so a supplier's
    /// newer quote with the same condition replaces an older one.
    /// </summary>
    public async Task OnRfqItemsCreatedAsync(IReadOnlyCollection<long> rfqItemIds, long? userId, CancellationToken cancellationToken = default)
    {
        if (rfqItemIds.Count == 0) return;
        var since = DateTime.UtcNow.AddDays(-AutoApplyDays);
        var accepted = await _db.Set<PendingSupplierQuote>()
            .Include(p => p.Certificates)
            .Where(p => p.Status == Accepted && !p.AutoApplyDisabled && p.ReviewedAt >= since && p.SupplierId != null && p.Price != null)
            .OrderBy(p => p.ReviewedAt) // oldest first: the newest quote is written last and wins
            .ToListAsync(cancellationToken);
        if (accepted.Count == 0) return;

        var supplierIds = accepted.Select(p => p.SupplierId!.Value).Distinct().ToList();
        var approved = (await _db.Set<Supplier>().AsNoTracking()
                .Where(s => supplierIds.Contains(s.Id) && s.Status == "Approved")
                .Select(s => s.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var items = await _db.Set<RFQItem>().AsNoTracking()
            .Include(i => i.RFQ).ThenInclude(r => r.Customer)
            .Include(i => i.PartNumber).ThenInclude(p => p.Alternatives)
            .Where(i => rfqItemIds.Contains(i.Id))
            .ToListAsync(cancellationToken);
        var index = IndexByPartKeys(items, accepted.SelectMany(PartKeys).Select(PnKey).ToHashSet());
        if (index.Count == 0) return;

        foreach (var row in accepted.Where(r => approved.Contains(r.SupplierId!.Value)))
        {
            var targets = TargetsFor(row, index);
            if (targets.Count == 0) continue;
            await WriteSupplierQuotesAsync(row, targets, row.ReviewedByUserId ?? userId);
            row.AppliedCount += targets.Count;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<List<SupplierLookupItem>> SearchSuppliersAsync(string? q)
    {
        var query = _db.Set<Supplier>().AsNoTracking().Where(s => s.Dependency != "Disabled" && s.Status == "Approved");
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(s => s.Name.Contains(q) || (s.Email != null && s.Email.Contains(q)));
        return await query.OrderBy(s => s.Name).Take(30)
            .Select(s => new SupplierLookupItem(s.Id, s.Name, s.Email))
            .ToListAsync();
    }

    // ──── Procurement Plus ────

    public async Task<List<PendingSupplierQuoteResponse>> GetLinesAsync(ICollection<long> ids)
    {
        if (ids.Count == 0) return new();
        var pendingIds = await _db.Set<PendingSupplierQuote>().AsNoTracking()
            .Where(p => ids.Contains(p.Id) && p.Status == Pending)
            .Select(p => p.Id)
            .ToListAsync();
        var reviewedIds = ids.Except(pendingIds).ToList();
        return (await GetPendingAsync(pendingIds)).Concat(await GetReviewedAsync(reviewedIds)).ToList();
    }

    public async Task<Dictionary<string, long>> MatchSuppliersByEmailAsync(IEnumerable<string?> emails, bool addDomainContacts = false)
    {
        var found = await FindSuppliersByEmailAsync(emails);
        if (addDomainContacts)
            // Found by company domain (y@abc.com -> the supplier with x@abc.com): y@abc.com becomes a contact too.
            foreach (var (email, match) in found.Where(f => f.Value.ByDomain))
                await AddSenderContactAsync(match.Supplier.Id, email);
        return found.ToDictionary(e => e.Key, e => e.Value.Supplier.Id, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<(bool Added, string? HeldBy)> AddEmailToSupplierAsync(long supplierId, string? email)
    {
        var normalized = SupplierEmails.Normalize(email);
        if (normalized == null) return (false, null);
        // Only an exact holder blocks it - another supplier on the same domain does not.
        var owner = (await FindSuppliersByEmailAsync([normalized], byDomain: false)).Values.FirstOrDefault()?.Supplier;
        if (owner != null) return (false, owner.Id == supplierId ? null : owner.Name);
        return (await AddSenderContactAsync(supplierId, normalized) != null, null);
    }

    // ──── Helpers ────

    /// <summary>Lines read from email — the Email Quotes page never lists Procurement Plus lines.</summary>
    private IQueryable<PendingSupplierQuote> EmailLines() =>
        _db.Set<PendingSupplierQuote>().Where(p => p.Source == null);

    private static string PnKey(string? partNumber) => Whitespace.Replace(partNumber ?? "", "").ToUpperInvariant();
    private static string CondKey(string? condition) => (condition ?? "").Trim().ToUpperInvariant();
    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? Trunc(string? value, int max) => value == null || value.Length <= max ? value : value[..max];

    /// <summary>"$1,250.00" → 1250.00; first number in the text, commas ignored.</summary>
    private static decimal? ParseDecimal(string? text)
    {
        var m = NumberPattern.Match((text ?? "").Replace(",", ""));
        return m.Success && decimal.TryParse(m.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static double? ParseDouble(string? text)
    {
        var m = NumberPattern.Match((text ?? "").Replace(",", ""));
        return m.Success && double.TryParse(m.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static readonly string[] TagDateFormats =
    {
        "yyyy-MM-dd", "MM/dd/yyyy", "M/d/yyyy", "dd-MMM-yyyy", "d-MMM-yyyy", "dd MMM yyyy", "d MMM yyyy",
        "MMM dd yyyy", "MMM d yyyy", "MMM dd, yyyy", "MMMM d, yyyy", "dd.MM.yyyy", "yyyy/MM/dd",
    };
    private static readonly string[] TagMonthFormats = { "MM/yyyy", "M/yyyy", "MM-yyyy", "MMM yyyy", "MMMM yyyy", "MMM-yyyy", "yyyy-MM" };

    /// <summary>
    /// "03/15/2024", "12-JAN-2024" → that day; "03/2024", "JAN 2024" → the 1st of the month;
    /// "2023" → 1 Jan 2023. The text as written is kept in TagDateRaw either way.
    /// </summary>
    private static DateOnly? ParseTagDate(string? text)
    {
        var t = Regex.Replace(text ?? "", @"^\s*(tag(ged)?\s*(date)?|td)\s*[:\-]?\s*", "", RegexOptions.IgnoreCase).Trim();
        if (t.Length == 0) return null;
        var ci = CultureInfo.InvariantCulture;
        if (DateOnly.TryParseExact(t, TagDateFormats, ci, DateTimeStyles.AllowWhiteSpaces, out var d)) return d;
        if (DateOnly.TryParseExact(t, TagMonthFormats, ci, DateTimeStyles.AllowWhiteSpaces, out var m)) return m;
        var year = Regex.Match(t, @"^(19|20)\d{2}$");
        if (year.Success) return new DateOnly(int.Parse(year.Value), 1, 1);
        return DateOnly.TryParse(t, ci, DateTimeStyles.AllowWhiteSpaces, out var any) ? any : null;
    }

    private static DateTime? ParseDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        // RFC 2822 dates often end in a zone comment like "(UTC)" that the parser rejects.
        var cleaned = Regex.Replace(text, @"\s*\([^)]*\)\s*$", "");
        return DateTimeOffset.TryParse(cleaned, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d)
            ? d.UtcDateTime
            : null;
    }
}
