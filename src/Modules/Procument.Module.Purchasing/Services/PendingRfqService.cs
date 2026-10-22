using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Procument.Module.Catalog.Entities;
using Procument.Module.Catalog.Services;
using Procument.Module.Identity.Entities;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Entities;
using Procument.Module.RFQ.DTOs;
using Procument.Module.RFQ.Entities;
using Procument.Module.RFQ.Services;
using Procument.Shared.Services;

namespace Procument.Module.Purchasing.Services;

public interface IPendingRfqService
{
    Task<ImportPendingRfqsResponse> ImportAsync(ImportPendingRfqsRequest request);
    Task<KnownEmailResponse> GetKnownAsync(string messageId);
    Task<PendingRfqPageResponse> GetPendingPageAsync(string? search, int page, int pageSize, string? mailbox = null);
    Task<PendingRfqPageResponse> GetAcceptedPageAsync(int? days, long? reviewedByUserId, string? search, int page, int pageSize, string? mailbox = null);
    Task<PendingRfqPageResponse> GetRemovedPageAsync(int? days, long? reviewedByUserId, string? search, int page, int pageSize, string? mailbox = null);
    Task<List<MailboxOption>> GetMailboxesAsync();
    Task<List<PendingRfqItemResponse>> GetExportAsync(DateTime from, DateTime to, string? mailbox = null);
    Task<PendingRfqItemResponse?> UpdateAsync(long id, UpdatePendingRfqItemRequest request);
    Task<int> UpdateEmailAsync(UpdatePendingRfqEmailRequest request);
    Task<SetPendingCustomerResponse> SetCustomerAsync(List<long> ids, long? customerId);
    Task<AcceptPendingRfqsResponse> AcceptAsync(List<long> ids, long userId);
    Task<int> RemoveAsync(List<long> ids, long userId);
    Task<int> RemoveAllPendingAsync(long userId);
    Task<int> RestoreAsync(List<long> ids);
    Task<List<CustomerLookupItem>> SearchCustomersAsync(string? q);
    Task<PendingQuoteEmailResponse?> GetEmailAsync(long emailId);
    Task<(Stream stream, string fileName, string contentType)?> GetAttachmentAsync(long emailId, long attachmentId);
}

/// <summary>
/// Email RFQs: customer RFQ lines MailFlow's CSV Export read from email, reviewed on the Email RFQs page.
/// Accept creates a new RFQ (header + items) for the email's customer through RFQService.BulkImportAsync,
/// owned by the user who accepted. Same life cycle as Email Quotes: Pending → Accepted / Removed, removed
/// rows kept as tombstones so MailFlow never imports that email again.
/// </summary>
public class PendingRfqService : IPendingRfqService
{
    private const string Pending = "Pending";
    private const string Accepted = "Accepted";
    private const string Removed = "Removed";
    /// <summary>No deadline in the email (MailFlow normally sends received + 3 days) → this many days after it arrived.</summary>
    public const int DefaultDeadlineDays = 3;
    public const int MaxEmailsPerPage = 100;

    private static readonly Regex NumberPattern = new(@"\d+(?:\.\d+)?", RegexOptions.Compiled);
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);
    private static readonly string[] DeadlineFormats =
        { "MM/dd/yyyy", "M/d/yyyy", "yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "dd.MM.yyyy", "dd-MMM-yyyy", "d MMM yyyy" };

    private readonly DbContext _db;
    private readonly IRFQService _rfqService;
    private readonly ILogger<PendingRfqService> _logger;

    public PendingRfqService(DbContext db, IRFQService rfqService, ILogger<PendingRfqService> logger)
    {
        _db = db;
        _rfqService = rfqService;
        _logger = logger;
    }

    // ──── Import (MailFlow) ────

    public async Task<ImportPendingRfqsResponse> ImportAsync(ImportPendingRfqsRequest request)
    {
        var messageId = Trunc(request.MessageId?.Trim(), 500);
        var subject = Trunc(request.Subject?.Trim(), 500);
        var customerEmail = Trunc(SupplierEmails.Normalize(request.CustomerEmail) ?? NullIfBlank(request.CustomerEmail), 320);
        var emailDate = ParseDate(request.Date);

        var existing = await (!string.IsNullOrEmpty(messageId)
                ? _db.Set<PendingRfqItem>().Where(p => p.MessageId == messageId)
                : _db.Set<PendingRfqItem>().Where(p =>
                    p.CustomerEmail == customerEmail && p.EmailSubject == subject && p.EmailDate == emailDate))
            .ToListAsync();

        // A line already seen — pending, accepted or removed — is never imported twice, and an email
        // whose lines were all removed takes nothing new at all (same rule as Email Quotes).
        var active = existing.Where(p => p.Status != Removed).ToList();
        var emailRemoved = existing.Count > 0 && active.Count == 0;
        var seenParts = existing.Select(p => PnKey(p.PartNumber)).ToHashSet();
        var items = emailRemoved
            ? []
            : request.Items
                .Where(i => !string.IsNullOrWhiteSpace(i.PartNumber) && seenParts.Add(PnKey(i.PartNumber)))
                .ToList();

        // One stored email per message: reuse it (also one Email Quotes stored), or store it now.
        var emailId = existing.Select(p => p.EmailId).FirstOrDefault(id => id != null);
        if (emailId == null && !string.IsNullOrEmpty(messageId))
            emailId = await _db.Set<PendingQuoteEmail>().Where(e => e.MessageId == messageId)
                .Select(e => (long?)e.Id).FirstOrDefaultAsync();
        var emailAttached = false;
        if (emailId == null && request.Email != null && !emailRemoved)
        {
            emailId = (await PendingEmailStore.SaveAsync(_db, request.Email, messageId, subject, customerEmail, emailDate)).Id;
            emailAttached = active.Count > 0;
        }
        if (emailId != null)
            foreach (var row in active.Where(r => r.EmailId == null)) row.EmailId = emailId;

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
            return new ImportPendingRfqsResponse { Duplicate = true, EmailAttached = emailAttached };
        }

        var customer = await MatchCustomerAsync(customerEmail, request.CustomerName);
        var rows = items
            .Select(i => new PendingRfqItem
            {
                MessageId = messageId,
                EmailSubject = subject,
                EmailDate = emailDate,
                CustomerEmail = customerEmail,
                CustomerNameRaw = Trunc(NullIfBlank(request.CustomerName), 300),
                Mailbox = mailbox,
                MailboxLabel = mailboxLabel,
                CustomerId = customer?.Id,
                RfqName = Trunc(NullIfBlank(i.RfqName) ?? subject ?? "RFQ", 300)!,
                DeadlineRaw = Trunc(NullIfBlank(i.Deadline), 100),
                Deadline = ParseDeadline(i.Deadline),
                PartNumber = Trunc(i.PartNumber!.Trim(), 200)!,
                Description = Trunc(NullIfBlank(i.Description), 1000),
                Qty = ParseDouble(i.Qty),
                Condition = Trunc(NullIfBlank(i.Condition)?.ToUpperInvariant(), 100),
                Priority = Trunc(NullIfBlank(i.Priority), 100),
                Alternatives = Trunc(JoinAlternatives(i.Alternatives), 1000),
                Remark = Trunc(NullIfBlank(i.Remark), 2000),
                EmailId = emailId,
            })
            .ToList();

        _db.Set<PendingRfqItem>().AddRange(rows);
        await _db.SaveChangesAsync();
        return new ImportPendingRfqsResponse
        {
            Imported = rows.Count,
            CustomerId = customer?.Id,
            CustomerName = customer?.Name,
        };
    }

    /// <summary>Known = Procument has lines from this email, so MailFlow skips it (removed ones too).</summary>
    public async Task<KnownEmailResponse> GetKnownAsync(string messageId)
    {
        var id = Trunc(messageId.Trim(), 500);
        var rows = _db.Set<PendingRfqItem>().Where(p => p.MessageId == id);
        return new KnownEmailResponse
        {
            Known = await rows.AnyAsync(),
            HasEmail = await rows.AnyAsync(p => p.EmailId != null || p.Status == Removed),
            NeedsReread = false,
        };
    }

    public Task<PendingQuoteEmailResponse?> GetEmailAsync(long emailId) => PendingEmailStore.GetAsync(_db, emailId);

    public Task<(Stream stream, string fileName, string contentType)?> GetAttachmentAsync(long emailId, long attachmentId) =>
        PendingEmailStore.GetAttachmentAsync(_db, emailId, attachmentId);

    // ──── Customer ↔ sender email ────

    private sealed record EmailMatch(Customer Customer, bool ByDomain);

    /// <summary>Email first (exact address, then same company domain), then the normalised name.</summary>
    private async Task<Customer?> MatchCustomerAsync(string? email, string? name)
    {
        var byEmail = (await FindCustomersByEmailAsync([email])).Values.FirstOrDefault();
        if (byEmail != null)
        {
            // Found by company domain: this address becomes one more contact person.
            if (byEmail.ByDomain) await AddSenderContactAsync(byEmail.Customer.Id, SupplierEmails.Normalize(email)!);
            return byEmail.Customer;
        }
        if (string.IsNullOrWhiteSpace(name) || name.Contains('@')) return null;
        var key = SupplierNameNormalizer.Key(name);
        if (string.IsNullOrEmpty(key)) return null;
        var candidates = await _db.Set<Customer>().AsNoTracking().Where(c => c.IsActive)
            .Select(c => new { c.Id, c.Name }).ToListAsync();
        var match = candidates.FirstOrDefault(c => SupplierNameNormalizer.Key(c.Name) == key);
        return match == null ? null : new Customer { Id = match.Id, Name = match.Name };
    }

    /// <summary>
    /// The active customer for each address: one holding the exact address (Email, Emails or a contact),
    /// else (byDomain) one using the same company domain — free-mail domains never match by domain.
    /// SQL Contains only pre-filters; the exact check runs on the parsed addresses.
    /// </summary>
    private async Task<Dictionary<string, EmailMatch>> FindCustomersByEmailAsync(IEnumerable<string?> emails, bool byDomain = true)
    {
        var usable = _db.Set<Customer>().AsNoTracking().Where(c => c.IsActive);
        var result = new Dictionary<string, EmailMatch>(StringComparer.OrdinalIgnoreCase);
        foreach (var email in emails.Select(SupplierEmails.Normalize).OfType<string>().Distinct())
        {
            var exact = (await usable
                    .Where(c => (c.Email != null && c.Email.Contains(email)) || (c.Emails != null && c.Emails.Contains(email))
                        || (c.Contacts != null && c.Contacts.Contains(email)))
                    .ToListAsync())
                .Where(c => CustomerEmails.AllEmails(c).Contains(email, StringComparer.OrdinalIgnoreCase))
                .OrderBy(c => c.Id)
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
                    .Where(c => (c.Email != null && c.Email.Contains(at)) || (c.Emails != null && c.Emails.Contains(at))
                        || (c.Contacts != null && c.Contacts.Contains(at)))
                    .ToListAsync())
                .Where(c => CustomerEmails.HasDomain(c, domain)) // LIKE also hits "@abc.com.tr"
                .OrderBy(c => c.Id)
                .FirstOrDefault();
            if (sameDomain != null) result[email] = new EmailMatch(sameDomain, true);
        }
        return result;
    }

    /// <summary>Adds the address to the customer as a contact person named after its username, unless it is there.</summary>
    private async Task<string?> AddSenderContactAsync(long customerId, string email)
    {
        var customer = await _db.Set<Customer>().FirstOrDefaultAsync(c => c.Id == customerId);
        if (customer == null) return null;
        var target = CustomerEmails.AddEmail(customer, email, email[..email.IndexOf('@')]);
        if (target != null) await _db.SaveChangesAsync();
        return target;
    }

    public async Task<SetPendingCustomerResponse> SetCustomerAsync(List<long> ids, long? customerId)
    {
        if (customerId != null && !await _db.Set<Customer>().AnyAsync(c => c.Id == customerId))
            throw new Exception("Customer not found.");
        var rows = await _db.Set<PendingRfqItem>().Where(p => ids.Contains(p.Id) && p.Status == Pending).ToListAsync();
        foreach (var row in rows) row.CustomerId = customerId;
        await _db.SaveChangesAsync();

        var result = new SetPendingCustomerResponse { Updated = rows.Count };
        var senderEmail = rows.Select(r => r.CustomerEmail).FirstOrDefault(e => !string.IsNullOrEmpty(e));
        if (customerId != null) await LinkSenderEmailAsync(customerId.Value, senderEmail, result);
        return result;
    }

    /// <summary>
    /// Customer picked for an email (Sam: yes): the sender's address goes on that customer's contacts unless
    /// it is there already, and other pending lines from that address without a customer get the same one.
    /// When another customer already holds the address it is not added twice — the response names it.
    /// </summary>
    private async Task LinkSenderEmailAsync(long customerId, string? senderEmail, SetPendingCustomerResponse result)
    {
        var email = SupplierEmails.Normalize(senderEmail);
        if (email == null) return;

        var owner = (await FindCustomersByEmailAsync([email], byDomain: false)).Values.FirstOrDefault()?.Customer;
        if (owner != null && owner.Id != customerId)
        {
            result.EmailOnOtherCustomer = owner.Name;
            return;
        }
        if (owner == null)
        {
            result.EmailTarget = await AddSenderContactAsync(customerId, email);
            if (result.EmailTarget != null) result.EmailAdded = email;
        }

        result.OtherLinesLinked += await _db.Set<PendingRfqItem>()
            .Where(p => p.Status == Pending && p.CustomerId == null && p.CustomerEmail == email)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.CustomerId, (long?)customerId));
    }

    /// <summary>Active customers by name, code or email, for the customer picker.</summary>
    public async Task<List<CustomerLookupItem>> SearchCustomersAsync(string? q)
    {
        var query = _db.Set<Customer>().AsNoTracking().Where(c => c.IsActive);
        var term = q?.Trim();
        if (!string.IsNullOrEmpty(term))
            query = query.Where(c => c.Name.Contains(term)
                || (c.CustomerCode != null && c.CustomerCode.Contains(term))
                || (c.Email != null && c.Email.Contains(term))
                || (c.Emails != null && c.Emails.Contains(term))
                || (c.Contacts != null && c.Contacts.Contains(term)));
        return await query.OrderBy(c => c.Name).Take(20)
            .Select(c => new CustomerLookupItem(c.Id, c.Name, c.CustomerCode, c.Email))
            .ToListAsync();
    }

    // ──── Lists ────

    private sealed record EmailPage(List<long> Ids, int Page, int PageSize, int TotalEmails, int TotalLines);

    /// <summary>Pages lines by their source email, so one email is never split across pages (same as Email Quotes).</summary>
    private static async Task<EmailPage> PageByEmailAsync(IQueryable<PendingRfqItem> query, bool byReview, int page, int pageSize)
    {
        pageSize = Math.Clamp(pageSize, 1, MaxEmailsPerPage);
        var heads = await query
            .Select(p => new { p.Id, p.MessageId, p.CustomerEmail, p.EmailSubject, p.EmailDate, p.CreatedAt, p.ReviewedAt })
            .ToListAsync();
        var emails = heads
            .GroupBy(h => h.MessageId ?? $"{h.CustomerEmail}|{h.EmailSubject}|{h.EmailDate:O}")
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

    /// <summary>Pending lines, one page of emails; a search keeps lines whose P/N, customer or subject contains it.</summary>
    public async Task<PendingRfqPageResponse> GetPendingPageAsync(string? search, int page, int pageSize, string? mailbox = null)
    {
        var all = ForMailbox(_db.Set<PendingRfqItem>().AsNoTracking().Where(p => p.Status == Pending), mailbox, p => p.Mailbox);
        var query = Search(all, search);
        var paged = await PageByEmailAsync(query, false, page, pageSize);
        return new PendingRfqPageResponse
        {
            Items = await BuildAsync(paged.Ids),
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalEmails = paged.TotalEmails,
            TotalLines = paged.TotalLines,
            PendingTotal = query == all ? paged.TotalLines : await all.CountAsync(),
        };
    }

    public Task<PendingRfqPageResponse> GetAcceptedPageAsync(int? days, long? reviewedByUserId, string? search, int page, int pageSize, string? mailbox = null) =>
        GetReviewedPageAsync(Accepted, days, reviewedByUserId, search, page, pageSize, mailbox);

    public Task<PendingRfqPageResponse> GetRemovedPageAsync(int? days, long? reviewedByUserId, string? search, int page, int pageSize, string? mailbox = null) =>
        GetReviewedPageAsync(Removed, days, reviewedByUserId, search, page, pageSize, mailbox);

    /// <summary>
    /// CSV export: pending and accepted lines (not removed) of emails sent from <paramref name="from"/> up to
    /// <paramref name="to"/> (UTC, to exclusive; arrival time when the email had no date), oldest email first.
    /// </summary>
    public async Task<List<PendingRfqItemResponse>> GetExportAsync(DateTime from, DateTime to, string? mailbox = null)
    {
        var ids = await ForMailbox(_db.Set<PendingRfqItem>().AsNoTracking().Where(p => p.Status != Removed), mailbox, p => p.Mailbox)
            .Where(p => (p.EmailDate ?? p.CreatedAt) >= from && (p.EmailDate ?? p.CreatedAt) < to)
            .Select(p => p.Id)
            .ToListAsync();
        return (await BuildAsync(ids))
            .OrderBy(r => r.EmailDate ?? r.CreatedAt).ThenBy(r => r.Id)
            .ToList();
    }

    /// <summary>The mailboxes RFQ lines came in through, for the page's mailbox dropdown.</summary>
    public async Task<List<MailboxOption>> GetMailboxesAsync() =>
        (await _db.Set<PendingRfqItem>().AsNoTracking()
            .GroupBy(p => new { p.Mailbox, p.MailboxLabel })
            .Select(g => new { g.Key.Mailbox, g.Key.MailboxLabel, Lines = g.Count() })
            .ToListAsync())
        .GroupBy(m => m.Mailbox ?? "-")
        .Select(g => new MailboxOption(g.Key, g.Key == "-" ? "Unknown mailbox (older emails)" : g.Select(x => x.MailboxLabel).FirstOrDefault(l => l != null) ?? g.Key, g.Sum(x => x.Lines)))
        .OrderBy(m => m.Mailbox == "-" ? 1 : 0).ThenBy(m => m.Label)
        .ToList();

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

    private async Task<PendingRfqPageResponse> GetReviewedPageAsync(
        string status, int? days, long? reviewedByUserId, string? search, int page, int pageSize, string? mailbox)
    {
        var inPeriod = ForMailbox(_db.Set<PendingRfqItem>().AsNoTracking().Where(p => p.Status == status), mailbox, p => p.Mailbox);
        if (days is > 0)
        {
            var since = DateTime.UtcNow.AddDays(-days.Value);
            inPeriod = inPeriod.Where(p => p.ReviewedAt >= since);
        }

        var reviewerIds = await inPeriod.Where(p => p.ReviewedByUserId != null)
            .Select(p => p.ReviewedByUserId!.Value).Distinct().ToListAsync();
        var reviewers = await _db.Set<User>().AsNoTracking()
            .Where(u => reviewerIds.Contains(u.Id))
            .OrderBy(u => u.Name)
            .Select(u => new QuoteReviewerOption(u.Id, u.Name))
            .ToListAsync();

        var query = inPeriod;
        if (reviewedByUserId != null) query = query.Where(p => p.ReviewedByUserId == reviewedByUserId);
        query = Search(query, search);
        var paged = await PageByEmailAsync(query, true, page, pageSize);
        return new PendingRfqPageResponse
        {
            Items = await BuildAsync(paged.Ids),
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalEmails = paged.TotalEmails,
            TotalLines = paged.TotalLines,
            Reviewers = reviewers,
        };
    }

    private static IQueryable<PendingRfqItem> Search(IQueryable<PendingRfqItem> query, string? search)
    {
        var q = search?.Trim();
        if (string.IsNullOrEmpty(q)) return query;
        return query.Where(p => p.PartNumber.Contains(q)
            || (p.Alternatives != null && p.Alternatives.Contains(q))
            || p.RfqName.Contains(q)
            || (p.CustomerNameRaw != null && p.CustomerNameRaw.Contains(q))
            || (p.CustomerEmail != null && p.CustomerEmail.Contains(q))
            || (p.EmailSubject != null && p.EmailSubject.Contains(q))
            || (p.Customer != null && p.Customer.Name.Contains(q)));
    }

    /// <summary>Builds these lines for the panels (a page, or one line after an edit).</summary>
    private async Task<List<PendingRfqItemResponse>> BuildAsync(ICollection<long> ids)
    {
        if (ids.Count == 0) return new();
        var rows = await _db.Set<PendingRfqItem>().AsNoTracking()
            .Include(p => p.Customer)
            .Where(p => ids.Contains(p.Id))
            .OrderBy(p => p.Id)
            .ToListAsync();

        var userIds = rows.Where(r => r.ReviewedByUserId != null).Select(r => r.ReviewedByUserId!.Value).Distinct().ToList();
        var userNames = await _db.Set<User>().AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name);
        var attachmentNames = await PendingEmailStore.AttachmentNamesAsync(_db, rows.Select(r => r.EmailId));
        var pendingRows = rows.Where(r => r.Status == Pending).ToList();
        var plannedNames = await PlannedNamesAsync(pendingRows);
        var pendingNames = plannedNames.Values.Distinct().ToList();
        var takenNames = (await _db.Set<RFQHeader>().AsNoTracking()
                .Where(h => pendingNames.Contains(h.Name))
                .Select(h => h.Name)
                .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return rows.Select(r => new PendingRfqItemResponse
        {
            Id = r.Id,
            MessageId = r.MessageId,
            EmailSubject = r.EmailSubject,
            EmailDate = r.EmailDate,
            CustomerEmail = r.CustomerEmail,
            CustomerNameRaw = r.CustomerNameRaw,
            Mailbox = r.Mailbox,
            MailboxLabel = r.MailboxLabel,
            CustomerId = r.CustomerId,
            CustomerName = r.Customer?.Name,
            CustomerCode = r.Customer?.CustomerCode,
            RfqName = r.RfqName,
            Deadline = r.Deadline,
            DeadlineRaw = r.DeadlineRaw,
            PartNumber = r.PartNumber,
            Description = r.Description,
            Qty = r.Qty,
            Condition = r.Condition,
            Priority = r.Priority,
            Alternatives = SplitAlternatives(r.Alternatives),
            Remark = r.Remark,
            Status = r.Status,
            CreatedAt = r.CreatedAt,
            EmailId = r.EmailId,
            AttachmentNames = r.EmailId != null && attachmentNames.TryGetValue(r.EmailId.Value, out var names) ? names : new(),
            ReviewedAt = r.ReviewedAt,
            ReviewedByUserId = r.ReviewedByUserId,
            ReviewedByName = r.ReviewedByUserId != null && userNames.TryGetValue(r.ReviewedByUserId.Value, out var n) ? n : null,
            RfqId = r.RfqId,
            CreatedRfqName = r.CreatedRfqName,
            PlannedRfqName = plannedNames.GetValueOrDefault(r.Id),
            PlannedDeadline = r.Status == Pending ? EffectiveDeadline(r) : null,
            RfqNameTaken = plannedNames.TryGetValue(r.Id, out var planned) && takenNames.Contains(planned),
        }).ToList();
    }

    // ──── RFQ names per deadline ────

    private static string EmailKey(PendingRfqItem r) => r.MessageId ?? $"{r.CustomerEmail}|{r.EmailSubject}|{r.EmailDate:O}";

    /// <summary>The line's deadline, or 3 days after the email arrived when the email gave none.</summary>
    private static DateTime EffectiveDeadline(PendingRfqItem r) =>
        (r.Deadline ?? (r.EmailDate ?? r.CreatedAt).AddDays(DefaultDeadlineDays)).Date;

    /// <summary>
    /// The RFQ name each line will be created under — the RFQ bulk import rule, per email: lines sharing an
    /// RFQ name but carrying different deadlines become ABA(1), ABA(2)... in deadline order. Numbering counts
    /// every pending and accepted line of the email (not just the ones given or ticked), so the names stay the
    /// same whichever lines are accepted first.
    /// </summary>
    private async Task<Dictionary<long, string>> PlannedNamesAsync(IReadOnlyCollection<PendingRfqItem> rows)
    {
        var result = new Dictionary<long, string>();
        if (rows.Count == 0) return result;

        var messageIds = rows.Where(r => r.MessageId != null).Select(r => r.MessageId).Distinct().ToList();
        var subjects = rows.Where(r => r.MessageId == null).Select(r => r.EmailSubject).Distinct().ToList();
        var siblings = await _db.Set<PendingRfqItem>().AsNoTracking()
            .Where(p => p.Status != Removed
                && (messageIds.Contains(p.MessageId) || (p.MessageId == null && subjects.Contains(p.EmailSubject))))
            .ToListAsync();

        var wanted = rows.Select(r => r.Id).ToHashSet();
        var emailKeys = rows.Select(EmailKey).ToHashSet();
        var lines = siblings.Concat(rows).DistinctBy(r => r.Id).Where(r => emailKeys.Contains(EmailKey(r)));
        foreach (var byName in lines.GroupBy(r => (Email: EmailKey(r), Name: r.RfqName.Trim().ToLowerInvariant())))
        {
            var name = byName.OrderBy(r => r.Id).First().RfqName.Trim(); // one spelling when the case differs
            var deadlines = byName.Select(EffectiveDeadline).Distinct().Order().ToList();
            foreach (var r in byName.Where(r => wanted.Contains(r.Id)))
                result[r.Id] = deadlines.Count == 1 ? name : $"{name}({deadlines.IndexOf(EffectiveDeadline(r)) + 1})";
        }
        return result;
    }

    // ──── Edit / remove / restore ────

    public async Task<PendingRfqItemResponse?> UpdateAsync(long id, UpdatePendingRfqItemRequest request)
    {
        var row = await _db.Set<PendingRfqItem>().FirstOrDefaultAsync(p => p.Id == id && p.Status == Pending);
        if (row == null) return null;
        if (string.IsNullOrWhiteSpace(request.PartNumber)) throw new Exception("Part number is required.");
        if (string.IsNullOrWhiteSpace(request.RfqName)) throw new Exception("RFQ name is required.");

        row.RfqName = Trunc(request.RfqName.Trim(), 300)!;
        row.Deadline = request.Deadline;
        row.PartNumber = Trunc(request.PartNumber.Trim(), 200)!;
        row.Description = Trunc(NullIfBlank(request.Description), 1000);
        row.Qty = request.Qty is > 0 ? request.Qty : null;
        row.Condition = Trunc(NullIfBlank(request.Condition)?.ToUpperInvariant(), 100);
        row.Priority = Trunc(NullIfBlank(request.Priority), 100);
        row.Alternatives = Trunc(JoinAlternatives(request.Alternatives), 1000);
        row.Remark = Trunc(NullIfBlank(request.Remark), 2000);
        await _db.SaveChangesAsync();
        return (await BuildAsync([id])).FirstOrDefault();
    }

    /// <summary>RFQ name and/or deadline for a whole email's pending lines (the fields on the email card).</summary>
    public async Task<int> UpdateEmailAsync(UpdatePendingRfqEmailRequest request)
    {
        var rows = await _db.Set<PendingRfqItem>().Where(p => request.Ids.Contains(p.Id) && p.Status == Pending).ToListAsync();
        var name = Trunc(NullIfBlank(request.RfqName), 300);
        foreach (var row in rows)
        {
            if (name != null) row.RfqName = name;
            if (request.Deadline != null) row.Deadline = request.Deadline;
        }
        await _db.SaveChangesAsync();
        return rows.Count;
    }

    public async Task<int> RemoveAsync(List<long> ids, long userId)
    {
        var rows = await _db.Set<PendingRfqItem>().Where(p => ids.Contains(p.Id) && p.Status == Pending).ToListAsync();
        foreach (var row in rows)
        {
            row.Status = Removed; // the tombstone stops MailFlow from importing this line again
            row.ReviewedAt = DateTime.UtcNow;
            row.ReviewedByUserId = userId;
        }
        await _db.SaveChangesAsync();
        return rows.Count;
    }

    public Task<int> RemoveAllPendingAsync(long userId)
    {
        var now = DateTime.UtcNow;
        return _db.Set<PendingRfqItem>()
            .Where(p => p.Status == Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, Removed)
                .SetProperty(p => p.ReviewedAt, (DateTime?)now)
                .SetProperty(p => p.ReviewedByUserId, (long?)userId));
    }

    public async Task<int> RestoreAsync(List<long> ids)
    {
        var rows = await _db.Set<PendingRfqItem>().Where(p => ids.Contains(p.Id) && p.Status == Removed).ToListAsync();
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

    /// <summary>
    /// Creates the RFQs: per email (and customer), one RFQ per RFQ name + deadline among the chosen lines —
    /// named ABA(1), ABA(2)... by deadline when the email splits one name (PlannedNamesAsync) —
    /// through RFQService.BulkImportAsync — part numbers, alternatives and descriptions are created on the
    /// fly, the Email Quotes 7-day auto-fill runs on the new items. Owner = the accepting user (Sam).
    /// A name that already exists is skipped by the import: those lines stay Pending with the reason.
    /// </summary>
    public async Task<AcceptPendingRfqsResponse> AcceptAsync(List<long> ids, long userId)
    {
        var result = new AcceptPendingRfqsResponse();
        var rows = await _db.Set<PendingRfqItem>().AsNoTracking()
            .Include(p => p.Customer)
            .Where(p => ids.Contains(p.Id) && p.Status == Pending)
            .OrderBy(p => p.Id)
            .ToListAsync();

        foreach (var missing in rows.Where(r => r.CustomerId == null))
            result.Failed.Add(Failure(missing, "Pick a customer first"));

        // Names are worked out over the whole email, so ABA(2) stays ABA(2) even when only its lines are ticked.
        var plannedNames = await PlannedNamesAsync(rows.Where(r => r.CustomerId != null).ToList());
        var emails = rows.Where(r => r.CustomerId != null)
            .GroupBy(r => (Email: EmailKey(r), Customer: r.CustomerId!.Value));
        foreach (var email in emails)
        {
            var lines = email.ToList();
            var first = lines[0];
            var received = first.EmailDate ?? first.CreatedAt;
            var groups = lines
                .GroupBy(r => (Name: plannedNames.GetValueOrDefault(r.Id) ?? r.RfqName.Trim(), Deadline: EffectiveDeadline(r)))
                .ToList();
            var request = new BulkImportRFQRequest
            {
                CustomerId = email.Key.Customer,
                UserId = userId,
                SenderMail = SupplierEmails.Normalize(first.CustomerEmail),
                Rfqs = groups.Select(g => new BulkImportRFQGroup
                {
                    RfqName = g.Key.Name,
                    ReceivedDate = received,
                    Deadline = g.Key.Deadline,
                    Items = g.Select(r => new BulkImportRFQItem
                    {
                        PartNumber = r.PartNumber,
                        Description = r.Description,
                        Qty = r.Qty is > 0 ? r.Qty.Value : 1,
                        Condition = r.Condition,
                        Priority = r.Priority,
                        Remark = r.Remark,
                        Alternatives = SplitAlternatives(r.Alternatives),
                    }).ToList(),
                }).ToList(),
            };

            BulkImportRFQResponse imported;
            try
            {
                imported = await _rfqService.BulkImportAsync(request, userId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Email RFQ accept failed for {Subject}", first.EmailSubject);
                _db.ChangeTracker.Clear();
                result.Failed.AddRange(lines.Select(r => Failure(r, ex.Message)));
                continue;
            }

            // BulkImportAsync clears the change tracker, so the lines are re-read before they're marked.
            var lineIds = lines.Select(r => r.Id).ToList();
            var tracked = await _db.Set<PendingRfqItem>().Where(p => lineIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
            var now = DateTime.UtcNow;
            foreach (var group in groups)
            {
                var created = imported.Results.FirstOrDefault(x =>
                    string.Equals(x.OriginalName, group.Key.Name, StringComparison.OrdinalIgnoreCase)
                    && x.Deadline?.Date == group.Key.Deadline);
                if (created == null || created.Skipped || created.RfqId == null)
                {
                    var reason = created?.Reason == null
                        ? "The RFQ was not created"
                        : $"{created.Reason} Rename the RFQ \"{group.Key.Name}\" and accept again.";
                    result.Failed.AddRange(group.Select(r => Failure(r, reason)));
                    continue;
                }
                foreach (var line in group)
                {
                    if (!tracked.TryGetValue(line.Id, out var row)) continue;
                    row.Status = Accepted;
                    row.ReviewedAt = now;
                    row.ReviewedByUserId = userId;
                    row.RfqId = created.RfqId;
                    row.CreatedRfqName = created.Name;
                    result.Accepted++;
                }
                result.Rfqs.Add(new CreatedRfqInfo(created.RfqId.Value, created.Name, first.Customer?.Name ?? "", created.ItemCount));
            }
            await _db.SaveChangesAsync();
        }
        return result;
    }

    // ──── Helpers ────

    private static PendingQuoteFailure Failure(PendingRfqItem row, string reason) =>
        new() { Id = row.Id, PartNumber = row.PartNumber, Reason = reason };

    private static string PnKey(string? partNumber) => Whitespace.Replace(partNumber ?? "", "").ToUpperInvariant();
    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? Trunc(string? value, int max) => value == null || value.Length <= max ? value : value[..max];

    private static string? JoinAlternatives(IEnumerable<string>? alternatives)
    {
        var list = SplitAlternatives(string.Join(",", alternatives ?? []));
        return list.Count == 0 ? null : string.Join(", ", list);
    }

    /// <summary>"A, B; C" → [A, B, C]; placeholders like "no" / "n/a" / "-" mean none.</summary>
    private static List<string> SplitAlternatives(string? raw) =>
        (raw ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(a => a.Length > 0 && !new[] { "no", "none", "n/a", "na", "-" }.Contains(a.ToLowerInvariant()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static double? ParseDouble(string? text)
    {
        var m = NumberPattern.Match((text ?? "").Replace(",", ""));
        return m.Success && double.TryParse(m.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) && v > 0 ? v : null;
    }

    /// <summary>MailFlow sends the deadline as MM/DD/YYYY; a few other plain formats are accepted too.</summary>
    private static DateTime? ParseDeadline(string? text)
    {
        var t = text?.Trim();
        if (string.IsNullOrEmpty(t)) return null;
        if (DateTime.TryParseExact(t, DeadlineFormats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var d))
            return d.Date;
        return DateTime.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var any) ? any.Date : null;
    }

    private static DateTime? ParseDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var cleaned = Regex.Replace(text, @"\s*\([^)]*\)\s*$", "");
        return DateTimeOffset.TryParse(cleaned, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d)
            ? d.UtcDateTime
            : null;
    }
}
