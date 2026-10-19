using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Catalog.Entities;
using Procument.Module.Identity.Entities;
using Procument.Module.Sales.DTOs;
using Procument.Module.Sales.Entities;

namespace Procument.Module.Sales.Services;

public interface ICustomerFollowUpService
{
    /// <summary>All active rows; message counts and unread flags are for <paramref name="userName"/>.</summary>
    Task<List<CustomerFollowUpResponse>> GetAllAsync(string userName);
    Task<CustomerFollowUpOptionsResponse> GetOptionsAsync();
    Task<CustomerFollowUpResponse> CreateAsync(CreateCustomerFollowUpRequest request, string userName);
    /// <summary>Returns null when the row does not exist. Throws ArgumentException for an unknown field or a bad value.</summary>
    Task<CustomerFollowUpResponse?> UpdateFieldAsync(long id, UpdateCustomerFollowUpFieldRequest request, string userName);
    /// <summary>Soft-deletes the row and permanently removes its messages and documents (rows and files).</summary>
    Task<bool> DeleteAsync(long id);

    /// <summary>Null when the row does not exist.</summary>
    Task<List<CustomerFollowUpDocumentResponse>?> GetDocumentsAsync(long id);
    /// <summary>Null when the row does not exist. Throws ArgumentException when no file is given or a file is not a PDF / photo.</summary>
    Task<List<CustomerFollowUpDocumentResponse>?> UploadDocumentsAsync(long id, IEnumerable<IFormFile> files, string userName);
    Task<(Stream Stream, string FileName, string MimeType)?> OpenDocumentAsync(long id, long documentId);
    Task<bool> DeleteDocumentAsync(long id, long documentId);

    /// <summary>Oldest first. Null when the row does not exist.</summary>
    Task<List<CustomerFollowUpMessageResponse>?> GetMessagesAsync(long id);
    /// <summary>
    /// Text plus optional PDFs / photos: the first file rides on the text message, each further file gets a
    /// message of its own. Files are saved as row documents. Null when the row does not exist.
    /// Throws ArgumentException for an empty message, too long text or a file that is not a PDF / photo.
    /// </summary>
    Task<List<CustomerFollowUpMessageResponse>?> SendMessageAsync(long id, string? text, IEnumerable<IFormFile> files, string userName);
    /// <summary>Deletes the message and its document. Only the sender may delete it.</summary>
    Task<DeleteMessageResult> DeleteMessageAsync(long id, long messageId, string userName);

    /// <summary>Rows that have messages or are marked unread, with the user's unread flag.</summary>
    Task<List<CustomerFollowUpMessageStatusResponse>> GetMessageStatusesAsync(string userName);
    /// <summary>The user has seen every message of the row (clears "marked unread"). False when the row does not exist.</summary>
    Task<bool> MarkMessagesReadAsync(long id, string userName);
    /// <summary>Flags the row unread for the user until they open its chat again. False when the row does not exist.</summary>
    Task<bool> MarkMessagesUnreadAsync(long id, string userName);
}

public enum DeleteMessageResult { Deleted, NotFound, NotSender }

public class CustomerFollowUpService : ICustomerFollowUpService
{
    public static readonly string[] DefaultResponseStatuses = ["Yes", "No", "Closed", "Waiting"];
    public static readonly string[] DefaultIssueStatuses =
        ["1st Follow Up", "2nd Follow Up", "3rd Follow Up", "Waiting for Feedback", "Open", "Closed"];

    public const int ShortTextMax = 100;
    public const int LongTextMax = 4000;

    internal const string DocumentRoot = "Documents/CustomerFollowUps";

    /// <summary>Accepted uploads (documents column and chat): PDFs and photos browsers can show.</summary>
    private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".bmp"] = "image/bmp",
    };

    private readonly DbContext _db;

    public CustomerFollowUpService(DbContext db)
    {
        _db = db;
    }

    public async Task<List<CustomerFollowUpResponse>> GetAllAsync(string userName)
    {
        var rows = await _db.Set<CustomerFollowUp>()
            .AsNoTracking()
            .Where(f => f.IsActive)
            .OrderByDescending(f => f.Id)
            .ToListAsync();
        // Documents of deleted rows are removed with the row, so every remaining document belongs to an active row.
        var documentCounts = await _db.Set<CustomerFollowUpDocument>()
            .AsNoTracking()
            .GroupBy(d => d.CustomerFollowUpId)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Id, g => g.Count);
        var chats = (await GetMessageStatusesAsync(userName)).ToDictionary(s => s.Id);
        return rows.Select(f =>
        {
            var chat = chats.GetValueOrDefault(f.Id);
            var response = Map(f, documentCounts.GetValueOrDefault(f.Id), chat?.MessageCount ?? 0);
            response.HasUnreadMessages = chat?.HasUnreadMessages ?? false;
            return response;
        }).ToList();
    }

    public async Task<CustomerFollowUpOptionsResponse> GetOptionsAsync()
    {
        var used = await _db.Set<CustomerFollowUp>()
            .AsNoTracking()
            .Where(f => f.IsActive)
            .Select(f => new { f.CustomerCode, f.Assign, f.ResponseStatus, f.IssueStatus })
            .ToListAsync();

        // Catalog customers by code; one without a code is listed by name.
        var catalogCustomers = await _db.Set<Customer>()
            .AsNoTracking()
            .Where(c => c.IsActive)
            .Select(c => c.CustomerCode != null && c.CustomerCode != "" ? c.CustomerCode : c.Name)
            .ToListAsync();

        var userNames = await _db.Set<User>()
            .AsNoTracking()
            .Where(u => u.IsActive)
            .Select(u => u.Name)
            .ToListAsync();

        return new CustomerFollowUpOptionsResponse
        {
            Customers = Merge(catalogCustomers, used.Select(u => u.CustomerCode), sort: true),
            Assignees = Merge(userNames, used.Select(u => u.Assign), sort: true),
            // Defaults keep their workflow order; values users added follow alphabetically.
            ResponseStatuses = Merge(DefaultResponseStatuses, used.Select(u => u.ResponseStatus), sort: false),
            IssueStatuses = Merge(DefaultIssueStatuses, used.Select(u => u.IssueStatus), sort: false),
        };
    }

    public async Task<CustomerFollowUpResponse> CreateAsync(CreateCustomerFollowUpRequest request, string userName)
    {
        var row = new CustomerFollowUp
        {
            DateOfRequest = ParseDate(request.DateOfRequest) ?? DateTime.UtcNow.Date,
            // New rows start as an open issue with no response yet.
            IssueStatus = "Open",
            ResponseStatus = "No",
            CreatedBy = userName,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };
        _db.Set<CustomerFollowUp>().Add(row);
        await _db.SaveChangesAsync();
        return Map(row, 0, 0);
    }

    public async Task<CustomerFollowUpResponse?> UpdateFieldAsync(long id, UpdateCustomerFollowUpFieldRequest request, string userName)
    {
        var row = await _db.Set<CustomerFollowUp>().FirstOrDefaultAsync(f => f.Id == id && f.IsActive);
        if (row == null) return null;

        var value = string.IsNullOrWhiteSpace(request.Value) ? null : request.Value.Trim();
        switch (request.Field)
        {
            case "customerCode": row.CustomerCode = Limit(value, ShortTextMax, "Customer Code"); break;
            case "partNumber": row.PartNumber = Limit(value, ShortTextMax, "Partnumber"); break;
            case "requestedItem": row.RequestedItem = Limit(value, LongTextMax, "Requested - To Do Item"); break;
            case "dateOfRequest": row.DateOfRequest = ParseDate(value); break;
            case "deadline": row.Deadline = ParseDate(value); break;
            case "assign": row.Assign = Limit(value, ShortTextMax, "Assign"); break;
            case "responseStatus": row.ResponseStatus = Limit(value, ShortTextMax, "Response Status"); break;
            case "issueStatus": row.IssueStatus = Limit(value, ShortTextMax, "Issue Status"); break;
            case "dateOfReply": row.DateOfReply = ParseDate(value); break;
            case "ourResponse": row.OurResponse = Limit(value, LongTextMax, "Our Response"); break;
            case "feedback": row.Feedback = Limit(value, LongTextMax, "Feedback"); break;
            default: throw new ArgumentException($"Unknown column '{request.Field}'.");
        }

        row.ModifiedBy = userName;
        row.ModifyAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        var documentCount = await _db.Set<CustomerFollowUpDocument>().CountAsync(d => d.CustomerFollowUpId == id);
        var messageCount = await _db.Set<CustomerFollowUpMessage>().CountAsync(m => m.CustomerFollowUpId == id);
        return Map(row, documentCount, messageCount);
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var row = await _db.Set<CustomerFollowUp>().FirstOrDefaultAsync(f => f.Id == id && f.IsActive);
        if (row == null) return false;
        row.IsActive = false;
        row.ModifyAt = DateTime.UtcNow;
        _db.Set<CustomerFollowUpMessage>().RemoveRange(
            await _db.Set<CustomerFollowUpMessage>().Where(m => m.CustomerFollowUpId == id).ToListAsync());
        _db.Set<CustomerFollowUpMessageRead>().RemoveRange(
            await _db.Set<CustomerFollowUpMessageRead>().Where(r => r.CustomerFollowUpId == id).ToListAsync());
        _db.Set<CustomerFollowUpDocument>().RemoveRange(
            await _db.Set<CustomerFollowUpDocument>().Where(d => d.CustomerFollowUpId == id).ToListAsync());
        await _db.SaveChangesAsync();

        // Files go only after the rows are gone, so a failed save never leaves rows pointing at missing files.
        // A locked file must not fail a delete that already succeeded — at worst it stays on disk.
        var folder = DocumentFolder(id);
        try
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return true;
    }

    // ── Documents ────────────────────────────────────────────────────────────

    public async Task<List<CustomerFollowUpDocumentResponse>?> GetDocumentsAsync(long id)
    {
        if (!await RowExistsAsync(id)) return null;
        var documents = await _db.Set<CustomerFollowUpDocument>()
            .AsNoTracking()
            .Where(d => d.CustomerFollowUpId == id)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync();
        return documents.Select(MapDocument).ToList();
    }

    public async Task<List<CustomerFollowUpDocumentResponse>?> UploadDocumentsAsync(long id, IEnumerable<IFormFile> files, string userName)
    {
        if (!await RowExistsAsync(id)) return null;

        var validFiles = ValidFiles(files);
        if (validFiles.Count == 0) throw new ArgumentException("No files were provided.");

        var documents = new List<CustomerFollowUpDocument>();
        foreach (var file in validFiles)
            documents.Add(await AddDocumentAsync(id, file, userName));
        await _db.SaveChangesAsync();
        return documents.Select(MapDocument).ToList();
    }

    /// <summary>Non-empty files; throws when one is not a PDF or a photo.</summary>
    private static List<IFormFile> ValidFiles(IEnumerable<IFormFile> files)
    {
        var validFiles = files.Where(f => f is { Length: > 0 }).ToList();
        if (validFiles.Any(f => !AllowedTypes.ContainsKey(Path.GetExtension(f.FileName))))
            throw new ArgumentException("Only PDF files and photos (JPG, PNG, GIF, WEBP, BMP) can be uploaded.");
        return validFiles;
    }

    /// <summary>Writes the file to disk and adds its document row (saved by the caller).</summary>
    private async Task<CustomerFollowUpDocument> AddDocumentAsync(long id, IFormFile file, string userName)
    {
        var folder = DocumentFolder(id);
        Directory.CreateDirectory(folder);
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var storedName = $"{Guid.NewGuid():N}{extension}";
        await using (var stream = File.Create(Path.Combine(folder, storedName)))
            await file.CopyToAsync(stream);

        var document = new CustomerFollowUpDocument
        {
            CustomerFollowUpId = id,
            FileName = storedName,
            OriginalFileName = SafeFileName(file.FileName),
            MimeType = AllowedTypes[extension],
            FileSizeBytes = file.Length,
            UploadedAt = DateTime.UtcNow,
            UploadedBy = userName,
        };
        _db.Set<CustomerFollowUpDocument>().Add(document);
        return document;
    }

    public async Task<(Stream Stream, string FileName, string MimeType)?> OpenDocumentAsync(long id, long documentId)
    {
        var document = await _db.Set<CustomerFollowUpDocument>()
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId && d.CustomerFollowUpId == id && d.CustomerFollowUp.IsActive);
        if (document == null) return null;

        var path = Path.Combine(DocumentFolder(id), document.FileName);
        return File.Exists(path)
            ? (File.OpenRead(path), document.OriginalFileName, document.MimeType ?? "application/pdf")
            : null;
    }

    public async Task<bool> DeleteDocumentAsync(long id, long documentId)
    {
        var document = await _db.Set<CustomerFollowUpDocument>()
            .FirstOrDefaultAsync(d => d.Id == documentId && d.CustomerFollowUpId == id && d.CustomerFollowUp.IsActive);
        if (document == null) return false;

        // Chat messages that carried this file keep its name and show it as deleted.
        foreach (var message in await _db.Set<CustomerFollowUpMessage>().Where(m => m.DocumentId == documentId).ToListAsync())
            message.DocumentId = null;
        _db.Set<CustomerFollowUpDocument>().Remove(document);
        await _db.SaveChangesAsync();

        DeleteDocumentFile(id, document);
        return true;
    }

    private static void DeleteDocumentFile(long id, CustomerFollowUpDocument document)
    {
        var path = Path.Combine(DocumentFolder(id), document.FileName);
        if (File.Exists(path)) File.Delete(path);
    }

    // ── Messages ─────────────────────────────────────────────────────────────

    public async Task<List<CustomerFollowUpMessageResponse>?> GetMessagesAsync(long id)
    {
        if (!await RowExistsAsync(id)) return null;
        var messages = await _db.Set<CustomerFollowUpMessage>()
            .AsNoTracking()
            .Include(m => m.Document)
            .Where(m => m.CustomerFollowUpId == id)
            .OrderBy(m => m.SentAt).ThenBy(m => m.Id)
            .ToListAsync();
        return messages.Select(MapMessage).ToList();
    }

    public async Task<List<CustomerFollowUpMessageResponse>?> SendMessageAsync(long id, string? text, IEnumerable<IFormFile> files, string userName)
    {
        if (!await RowExistsAsync(id)) return null;

        var body = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        Limit(body, LongTextMax, "A message");
        var validFiles = ValidFiles(files);
        if (body == null && validFiles.Count == 0) throw new ArgumentException("The message is empty.");

        var now = DateTime.UtcNow;
        var messages = new List<CustomerFollowUpMessage>();
        if (validFiles.Count == 0)
        {
            messages.Add(new CustomerFollowUpMessage { CustomerFollowUpId = id, Text = body, SentBy = userName, SentAt = now });
        }
        for (var i = 0; i < validFiles.Count; i++)
        {
            var document = await AddDocumentAsync(id, validFiles[i], userName);
            messages.Add(new CustomerFollowUpMessage
            {
                CustomerFollowUpId = id,
                Text = i == 0 ? body : null,
                Document = document,
                DocumentName = document.OriginalFileName,
                SentBy = userName,
                SentAt = now,
            });
        }
        _db.Set<CustomerFollowUpMessage>().AddRange(messages);
        await _db.SaveChangesAsync();
        return messages.Select(MapMessage).ToList();
    }

    public async Task<DeleteMessageResult> DeleteMessageAsync(long id, long messageId, string userName)
    {
        var message = await _db.Set<CustomerFollowUpMessage>()
            .Include(m => m.Document)
            .FirstOrDefaultAsync(m => m.Id == messageId && m.CustomerFollowUpId == id && m.CustomerFollowUp.IsActive);
        if (message == null) return DeleteMessageResult.NotFound;
        if (!string.Equals(message.SentBy, userName, StringComparison.OrdinalIgnoreCase)) return DeleteMessageResult.NotSender;

        var document = message.Document;
        _db.Set<CustomerFollowUpMessage>().Remove(message);
        if (document != null) _db.Set<CustomerFollowUpDocument>().Remove(document);
        await _db.SaveChangesAsync();

        if (document != null) DeleteDocumentFile(id, document);
        return DeleteMessageResult.Deleted;
    }

    // ── Read / unread (per user) ─────────────────────────────────────────────

    public async Task<List<CustomerFollowUpMessageStatusResponse>> GetMessageStatusesAsync(string userName)
    {
        // Messages of deleted rows are removed with the row, so these are all active rows.
        var chats = await _db.Set<CustomerFollowUpMessage>()
            .AsNoTracking()
            .GroupBy(m => m.CustomerFollowUpId)
            .Select(g => new
            {
                Id = g.Key,
                Count = g.Count(),
                // Newest message from someone else — the user's own messages never make a row unread.
                LastFromOthers = g.Max(m => m.SentBy != userName ? m.Id : 0L),
            })
            .ToListAsync();
        var reads = await _db.Set<CustomerFollowUpMessageRead>()
            .AsNoTracking()
            .Where(r => r.UserName == userName && r.CustomerFollowUp.IsActive)
            .ToDictionaryAsync(r => r.CustomerFollowUpId);

        var statuses = chats.Select(c =>
        {
            var read = reads.GetValueOrDefault(c.Id);
            return new CustomerFollowUpMessageStatusResponse
            {
                Id = c.Id,
                MessageCount = c.Count,
                HasUnreadMessages = read?.MarkedUnread == true || c.LastFromOthers > (read?.LastReadMessageId ?? 0),
            };
        }).ToList();
        // A row marked unread before it had any message.
        var listed = statuses.Select(s => s.Id).ToHashSet();
        statuses.AddRange(reads.Values
            .Where(r => r.MarkedUnread && !listed.Contains(r.CustomerFollowUpId))
            .Select(r => new CustomerFollowUpMessageStatusResponse { Id = r.CustomerFollowUpId, HasUnreadMessages = true }));
        return statuses;
    }

    public async Task<bool> MarkMessagesReadAsync(long id, string userName)
    {
        if (!await RowExistsAsync(id)) return false;
        var lastId = await _db.Set<CustomerFollowUpMessage>()
            .Where(m => m.CustomerFollowUpId == id)
            .MaxAsync(m => (long?)m.Id) ?? 0;
        var read = await GetOrAddReadAsync(id, userName);
        read.LastReadMessageId = Math.Max(read.LastReadMessageId, lastId);
        read.MarkedUnread = false;
        read.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> MarkMessagesUnreadAsync(long id, string userName)
    {
        if (!await RowExistsAsync(id)) return false;
        var read = await GetOrAddReadAsync(id, userName);
        read.MarkedUnread = true;
        read.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    private async Task<CustomerFollowUpMessageRead> GetOrAddReadAsync(long id, string userName)
    {
        var read = await _db.Set<CustomerFollowUpMessageRead>()
            .FirstOrDefaultAsync(r => r.CustomerFollowUpId == id && r.UserName == userName);
        if (read != null) return read;
        read = new CustomerFollowUpMessageRead { CustomerFollowUpId = id, UserName = userName };
        _db.Set<CustomerFollowUpMessageRead>().Add(read);
        return read;
    }

    private static CustomerFollowUpMessageResponse MapMessage(CustomerFollowUpMessage m) => new()
    {
        Id = m.Id,
        Text = m.Text,
        SentBy = m.SentBy,
        // Stored as UTC; marking it so the JSON carries a "Z" and browsers show the viewer's local time.
        SentAt = DateTime.SpecifyKind(m.SentAt, DateTimeKind.Utc),
        Document = m.Document == null ? null : MapDocument(m.Document),
        DocumentName = m.DocumentName,
    };

    private Task<bool> RowExistsAsync(long id) =>
        _db.Set<CustomerFollowUp>().AnyAsync(f => f.Id == id && f.IsActive);

    private static string DocumentFolder(long id) => Path.Combine(DocumentRoot, id.ToString());

    private static string SafeFileName(string? fileName)
    {
        var name = Path.GetFileName(fileName ?? "").Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
            name = name.Replace(invalid, '-');
        var extension = Path.GetExtension(name);
        if (string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(name))) name = "document" + extension;
        // Keep the extension when trimming a long name to the column's 255 characters.
        return name.Length <= 255 ? name : name[..(255 - extension.Length)] + extension;
    }

    private static CustomerFollowUpDocumentResponse MapDocument(CustomerFollowUpDocument d) => new()
    {
        Id = d.Id,
        OriginalFileName = d.OriginalFileName,
        MimeType = d.MimeType ?? "application/pdf",
        FileSizeBytes = d.FileSizeBytes,
        UploadedAt = d.UploadedAt,
        UploadedBy = d.UploadedBy,
    };

    private static List<string> Merge(IEnumerable<string> first, IEnumerable<string?> used, bool sort)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var head = first.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).Where(seen.Add).ToList();
        var extra = used.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!.Trim()).Where(seen.Add).ToList();
        var all = sort ? head.Concat(extra).OrderBy(v => v, StringComparer.OrdinalIgnoreCase) : head.Concat(extra.OrderBy(v => v, StringComparer.OrdinalIgnoreCase));
        return all.ToList();
    }

    private static string? Limit(string? value, int max, string label)
    {
        if (value != null && value.Length > max)
            throw new ArgumentException($"{label} can be at most {max} characters.");
        return value;
    }

    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = value.Trim();
        if (text.Length > 10) text = text[..10];
        if (DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;
        throw new ArgumentException($"'{value}' is not a valid date (expected yyyy-MM-dd).");
    }

    private static CustomerFollowUpResponse Map(CustomerFollowUp f, int documentCount, int messageCount) => new()
    {
        Id = f.Id,
        CustomerCode = f.CustomerCode,
        PartNumber = f.PartNumber,
        RequestedItem = f.RequestedItem,
        DateOfRequest = f.DateOfRequest,
        Deadline = f.Deadline,
        Assign = f.Assign,
        ResponseStatus = f.ResponseStatus,
        IssueStatus = f.IssueStatus,
        DateOfReply = f.DateOfReply,
        OurResponse = f.OurResponse,
        Feedback = f.Feedback,
        CreatedBy = f.CreatedBy,
        CreatedAt = f.CreatedAt,
        ModifiedBy = f.ModifiedBy,
        ModifyAt = f.ModifyAt,
        DocumentCount = documentCount,
        MessageCount = messageCount,
    };
}
