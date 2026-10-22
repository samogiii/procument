using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Catalog.Entities;
using Procument.Module.Identity.Entities;
using Procument.Module.Purchasing.Entities;
using Procument.Module.RFQ.DTOs;
using Procument.Module.RFQ.Entities;
using Procument.Module.RFQ.Services;
using Procument.Module.Sales.DTOs;
using Procument.Module.Sales.Entities;

namespace Procument.Module.Sales.Services;

public interface ICustomerFollowUpService
{
    /// <summary>All active rows; message counts and unread flags are for <paramref name="userName"/>.</summary>
    Task<List<CustomerFollowUpResponse>> GetAllAsync(string userName);
    /// <summary>One active row (null when it does not exist), e.g. to pick up a status the server changed.</summary>
    Task<CustomerFollowUpResponse?> GetByIdAsync(long id, string userName);
    Task<CustomerFollowUpOptionsResponse> GetOptionsAsync(string userName);
    Task<CustomerFollowUpResponse> CreateAsync(CreateCustomerFollowUpRequest request, string userName);
    /// <summary>Returns null when the row does not exist. Throws ArgumentException for an unknown field or a bad value.</summary>
    Task<CustomerFollowUpResponse?> UpdateFieldAsync(long id, UpdateCustomerFollowUpFieldRequest request, string userName);

    /// <summary>Links an existing RFQ (not "No Quote"), or unlinks with null. Null when the row does not exist.</summary>
    Task<CustomerFollowUpResponse?> SetRfqAsync(long id, long? rfqId, string userName);
    /// <summary>Creates a new RFQ owned by <paramref name="userId"/> and links it. Throws ArgumentException for bad input or a taken name.</summary>
    Task<CustomerFollowUpResponse?> CreateRfqAsync(long id, CreateCustomerFollowUpRfqRequest request, string userName, long userId);
    /// <summary>Replaces the row's Other Refs. Throws ArgumentException for an unknown type or a document that does not exist.</summary>
    Task<CustomerFollowUpResponse?> SetRefsAsync(long id, List<CustomerFollowUpRefKey> refs, string userName);
    /// <summary>One page of RFQs (never "No Quote") or of one Other Refs type, newest first.</summary>
    Task<CustomerFollowUpRefOptionsPage> SearchRefsAsync(string type, string? search, int skip, int take);
    /// <summary>Catalog customers for the new-RFQ form.</summary>
    Task<List<CustomerFollowUpCustomerOption>> GetRfqCustomersAsync();
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
    /// <summary>
    /// Menu Access feature naming the procurement users (no SuperAdmin bypass). Their edits move an Open
    /// Procurement Status to In Progress, and the page hides rows whose Procurement Status is Done from them.
    /// </summary>
    public const string ProcurementFeature = "followUpProcurement";

    public static readonly string[] DefaultCategories = ["RFQ", "Cert", "Order"];
    public static readonly string[] DefaultIssueStatuses =
        ["1st Follow Up", "2nd Follow Up", "3rd Follow Up", "Waiting for Feedback", "Open", "Closed"];

    public const string StatusOpen = "Open";
    public const string StatusInProgress = "In Progress";
    public const string StatusDone = "Done";
    /// <summary>Procurement Status and Sales Status — a fixed list, not free text.</summary>
    public static readonly string[] WorkStatuses = [StatusOpen, StatusInProgress, StatusDone];

    private const string NoQuoteStatus = "No Quote";
    public const int MaxRefPageSize = 50;
    /// <summary>A new RFQ without a deadline is due this many days from today (as in Email RFQs).</summary>
    private const int DefaultRfqDeadlineDays = 3;

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
    private readonly IRFQService _rfqService;

    public CustomerFollowUpService(DbContext db, IRFQService rfqService)
    {
        _db = db;
        _rfqService = rfqService;
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
        var rfqs = await RfqRefsAsync(rows.Where(f => f.RfqId != null).Select(f => f.RfqId!.Value));
        // Refs of deleted rows are removed with the row too.
        var refs = await RowRefsAsync(null);
        return rows.Select(f =>
        {
            var chat = chats.GetValueOrDefault(f.Id);
            var response = Map(f, documentCounts.GetValueOrDefault(f.Id), chat?.MessageCount ?? 0);
            response.HasUnreadMessages = chat?.HasUnreadMessages ?? false;
            response.Rfq = f.RfqId is { } rfqId ? rfqs.GetValueOrDefault(rfqId) : null;
            response.Refs = refs.GetValueOrDefault(f.Id) ?? [];
            return response;
        }).ToList();
    }

    public async Task<CustomerFollowUpResponse?> GetByIdAsync(long id, string userName)
    {
        var row = await _db.Set<CustomerFollowUp>().AsNoTracking().FirstOrDefaultAsync(f => f.Id == id && f.IsActive);
        if (row == null) return null;
        var response = await FullResponseAsync(row);
        response.HasUnreadMessages = (await GetMessageStatusesAsync(userName)).Any(s => s.Id == id && s.HasUnreadMessages);
        return response;
    }

    public async Task<CustomerFollowUpOptionsResponse> GetOptionsAsync(string userName)
    {
        var used = await _db.Set<CustomerFollowUp>()
            .AsNoTracking()
            .Where(f => f.IsActive)
            .Select(f => new { f.CustomerCode, f.Assign, f.Category, f.IssueStatus })
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
            Categories = Merge(DefaultCategories, used.Select(u => u.Category), sort: false),
            IssueStatuses = Merge(DefaultIssueStatuses, used.Select(u => u.IssueStatus), sort: false),
            WorkStatuses = [.. WorkStatuses],
            IsProcurementUser = await IsProcurementUserAsync(userName),
        };
    }

    public async Task<CustomerFollowUpResponse> CreateAsync(CreateCustomerFollowUpRequest request, string userName)
    {
        var row = new CustomerFollowUp
        {
            DateOfRequest = ParseDate(request.DateOfRequest) ?? DateTime.UtcNow.Date,
            // New rows start as an open issue that neither procurement nor sales has worked on yet.
            IssueStatus = "Open",
            ProcurementStatus = StatusOpen,
            SalesStatus = StatusOpen,
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
            case "category": row.Category = Limit(value, ShortTextMax, "Category"); break;
            case "partNumber": row.PartNumber = Limit(value, ShortTextMax, "Partnumber"); break;
            case "requestedItem": row.RequestedItem = Limit(value, LongTextMax, "Requested - To Do Item"); break;
            case "dateOfRequest": row.DateOfRequest = ParseDate(value); break;
            case "deadline": row.Deadline = ParseDate(value); break;
            case "assign": row.Assign = Limit(value, ShortTextMax, "Assign"); break;
            case "procurementStatus": row.ProcurementStatus = WorkStatus(value, "Procurement Status"); break;
            case "salesStatus": row.SalesStatus = WorkStatus(value, "Sales Status"); break;
            case "issueStatus": row.IssueStatus = Limit(value, ShortTextMax, "Issue Status"); break;
            case "dateOfReply": row.DateOfReply = ParseDate(value); break;
            case "ourResponse": row.OurResponse = Limit(value, LongTextMax, "Our Response"); break;
            case "feedback": row.Feedback = Limit(value, LongTextMax, "Feedback"); break;
            default: throw new ArgumentException($"Unknown column '{request.Field}'.");
        }

        // A procurement user who sets the status by hand gets exactly that status.
        if (request.Field != "procurementStatus") await MarkProcurementWorkAsync(row, userName);
        Touch(row, userName);
        await _db.SaveChangesAsync();
        return await FullResponseAsync(row);
    }

    // ── RFQ Ref# ─────────────────────────────────────────────────────────────

    public async Task<CustomerFollowUpResponse?> SetRfqAsync(long id, long? rfqId, string userName)
    {
        var row = await _db.Set<CustomerFollowUp>().FirstOrDefaultAsync(f => f.Id == id && f.IsActive);
        if (row == null) return null;

        if (rfqId != null)
        {
            var status = await _db.Set<RFQHeader>().AsNoTracking()
                .Where(r => r.Id == rfqId)
                .Select(r => new { r.Status })
                .FirstOrDefaultAsync()
                ?? throw new ArgumentException($"RFQ #{rfqId} does not exist.");
            if (status.Status == NoQuoteStatus) throw new ArgumentException("A No Quote RFQ cannot be linked.");
        }

        row.RfqId = rfqId;
        await MarkProcurementWorkAsync(row, userName);
        Touch(row, userName);
        await _db.SaveChangesAsync();
        return await FullResponseAsync(row);
    }

    public async Task<CustomerFollowUpResponse?> CreateRfqAsync(long id, CreateCustomerFollowUpRfqRequest request, string userName, long userId)
    {
        if (!await RowExistsAsync(id)) return null;

        var name = request.Name?.Trim() ?? "";
        if (name.Length == 0) throw new ArgumentException("RFQ name is required.");
        Limit(name, 200, "RFQ name");
        var items = (request.Items ?? [])
            .Where(i => !string.IsNullOrWhiteSpace(i.PartNumber))
            .Select(i => new BulkImportRFQItem
            {
                PartNumber = Limit(i.PartNumber.Trim(), ShortTextMax, "Part number")!,
                Description = string.IsNullOrWhiteSpace(i.Description) ? null : i.Description.Trim(),
                Qty = i.Qty > 0 ? i.Qty : 1,
            })
            .ToList();
        if (items.Count == 0) throw new ArgumentException("Add at least one part.");
        if (!await _db.Set<Customer>().AnyAsync(c => c.Id == request.CustomerId))
            throw new ArgumentException("Pick the customer.");

        var now = DateTime.UtcNow;
        var import = await _rfqService.BulkImportAsync(new BulkImportRFQRequest
        {
            CustomerId = request.CustomerId,
            UserId = userId,
            Rfqs =
            [
                new BulkImportRFQGroup
                {
                    RfqName = name,
                    ReceivedDate = now,
                    Deadline = ParseDate(request.Deadline) ?? now.Date.AddDays(DefaultRfqDeadlineDays),
                    Items = items,
                },
            ],
        }, userId);

        var result = import.Results.FirstOrDefault();
        if (result == null || result.Skipped || result.RfqId == null)
            throw new ArgumentException(result?.Reason == null ? "The RFQ could not be created." : $"RFQ '{name}': {result.Reason}");

        // The import clears the change tracker, so the row is read after it.
        var row = await _db.Set<CustomerFollowUp>().FirstAsync(f => f.Id == id);
        row.RfqId = result.RfqId;
        await MarkProcurementWorkAsync(row, userName);
        Touch(row, userName);
        await _db.SaveChangesAsync();
        return await FullResponseAsync(row);
    }

    public async Task<List<CustomerFollowUpCustomerOption>> GetRfqCustomersAsync()
    {
        var customers = await _db.Set<Customer>().AsNoTracking()
            .Where(c => c.IsActive)
            .Select(c => new CustomerFollowUpCustomerOption { Id = c.Id, Name = c.Name, CustomerCode = c.CustomerCode })
            .ToListAsync();
        // Customers with a code first, by code; the rest by name.
        return customers
            .OrderBy(c => string.IsNullOrWhiteSpace(c.CustomerCode))
            .ThenBy(c => c.CustomerCode ?? c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // ── Other Refs ───────────────────────────────────────────────────────────

    public async Task<CustomerFollowUpResponse?> SetRefsAsync(long id, List<CustomerFollowUpRefKey> refs, string userName)
    {
        var row = await _db.Set<CustomerFollowUp>().Include(f => f.Refs).FirstOrDefaultAsync(f => f.Id == id && f.IsActive);
        if (row == null) return null;

        var wanted = new HashSet<(string Type, long Id)>();
        foreach (var key in refs ?? [])
        {
            var type = CustomerFollowUpRefTypes.Normalize(key.Type) ?? throw new ArgumentException($"Unknown reference type '{key.Type}'.");
            wanted.Add((type, key.Id));
        }
        if (wanted.Count > 100) throw new ArgumentException("A row can have at most 100 references.");

        // Only newly added documents must exist — a linked one deleted since may stay until it is removed.
        var current = row.Refs.Select(r => (r.RefType, r.RefId)).ToHashSet();
        foreach (var group in wanted.Where(w => !current.Contains(w)).GroupBy(w => w.Type))
        {
            var found = await RefLabelsAsync(group.Key, group.Select(g => g.Id).ToList());
            var missing = group.FirstOrDefault(g => !found.ContainsKey(g.Id));
            if (missing != default) throw new ArgumentException($"{RefTypeLabel(group.Key)} #{missing.Id} does not exist.");
        }

        _db.Set<CustomerFollowUpRef>().RemoveRange(row.Refs.Where(r => !wanted.Contains((r.RefType, r.RefId))));
        var now = DateTime.UtcNow;
        foreach (var (type, refId) in wanted.Where(w => !current.Contains(w)))
        {
            _db.Set<CustomerFollowUpRef>().Add(new CustomerFollowUpRef
            {
                CustomerFollowUpId = id, RefType = type, RefId = refId, AddedBy = userName, AddedAt = now,
            });
        }

        await MarkProcurementWorkAsync(row, userName);
        Touch(row, userName);
        await _db.SaveChangesAsync();
        return await FullResponseAsync(row);
    }

    public async Task<CustomerFollowUpRefOptionsPage> SearchRefsAsync(string type, string? search, int skip, int take)
    {
        var q = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        skip = Math.Max(0, skip);
        take = Math.Clamp(take, 1, MaxRefPageSize);

        List<CustomerFollowUpRefOption> items;
        if (string.Equals(type, "Rfq", StringComparison.OrdinalIgnoreCase))
        {
            var query = _db.Set<RFQHeader>().AsNoTracking().Where(r => r.Status != NoQuoteStatus);
            if (q != null)
            {
                query = query.Where(r => r.Name.Contains(q)
                    || r.Customer.Name.Contains(q)
                    || (r.Customer.CustomerCode != null && r.Customer.CustomerCode.Contains(q))
                    || r.RFQItems.Any(i => i.PartNumber.Name.Contains(q)));
            }
            var page = await query.OrderByDescending(r => r.Id).Skip(skip).Take(take + 1)
                .Select(r => new
                {
                    r.Id, r.Name, r.Status, r.CreatedAt, r.LeadTime,
                    Customer = r.Customer.CustomerCode ?? r.Customer.Name,
                    Parts = r.RFQItems.OrderBy(i => i.Id).Select(i => i.PartNumber.Name).Take(4).ToList(),
                    PartCount = r.RFQItems.Count,
                })
                .ToListAsync();
            items = page.Select(r => new CustomerFollowUpRefOption
            {
                Id = r.Id,
                Label = r.Name,
                Status = r.Status,
                CreatedAt = r.CreatedAt,
                Detail = Join(r.Customer, $"Deadline {r.LeadTime:yyyy-MM-dd}",
                    r.PartCount == 0 ? null : string.Join(", ", r.Parts) + (r.PartCount > r.Parts.Count ? $" +{r.PartCount - r.Parts.Count}" : "")),
            }).ToList();
        }
        else
        {
            items = (CustomerFollowUpRefTypes.Normalize(type) ?? throw new ArgumentException($"Unknown reference type '{type}'.")) switch
            {
                CustomerFollowUpRefTypes.PurchaseOrder => await SearchPurchaseOrdersAsync(q, skip, take + 1),
                CustomerFollowUpRefTypes.Quote => await SearchQuotesAsync(q, skip, take + 1),
                CustomerFollowUpRefTypes.Invoice => await SearchInvoicesAsync(q, skip, take + 1),
                _ => await SearchFinalInvoicesAsync(q, skip, take + 1),
            };
        }

        return new CustomerFollowUpRefOptionsPage { Items = items.Take(take).ToList(), HasMore = items.Count > take };
    }

    private async Task<List<CustomerFollowUpRefOption>> SearchPurchaseOrdersAsync(string? q, int skip, int take)
    {
        var query = _db.Set<PurchaseOrder>().AsNoTracking();
        if (q != null)
            query = query.Where(p => p.PONumber.Contains(q) || p.Supplier.Name.Contains(q) || (p.Subject != null && p.Subject.Contains(q)));
        var page = await query.OrderByDescending(p => p.Id).Skip(skip).Take(take)
            .Select(p => new { p.Id, p.PONumber, p.Status, p.CreatedAt, p.Origin, Supplier = p.Supplier.Name, p.TotalAmount })
            .ToListAsync();
        return page.Select(p => new CustomerFollowUpRefOption
        {
            Id = p.Id,
            Label = p.PONumber,
            Status = p.Status,
            CreatedAt = p.CreatedAt,
            Detail = Join(p.Supplier, p.Origin == "Stock" ? "Stock PO" : null, Amount(p.TotalAmount)),
        }).ToList();
    }

    private async Task<List<CustomerFollowUpRefOption>> SearchQuotesAsync(string? q, int skip, int take)
    {
        var query = _db.Set<Quote>().AsNoTracking();
        if (q != null)
        {
            query = query.Where(x => x.QuoteNumber.Contains(q)
                || (x.B1QuoteNumber != null && x.B1QuoteNumber.Contains(q))
                || x.Customer.Name.Contains(q)
                || (x.Customer.CustomerCode != null && x.Customer.CustomerCode.Contains(q))
                || x.RFQ.Name.Contains(q));
        }
        var page = await query.OrderByDescending(x => x.Id).Skip(skip).Take(take)
            .Select(x => new { x.Id, x.QuoteNumber, x.B1QuoteNumber, x.Status, x.CreatedAt, Customer = x.Customer.CustomerCode ?? x.Customer.Name, Rfq = x.RFQ.Name })
            .ToListAsync();
        return page.Select(x => new CustomerFollowUpRefOption
        {
            Id = x.Id,
            Label = x.QuoteNumber,
            Status = x.Status,
            CreatedAt = x.CreatedAt,
            Detail = Join(x.Customer, x.B1QuoteNumber, $"RFQ {x.Rfq}"),
        }).ToList();
    }

    private async Task<List<CustomerFollowUpRefOption>> SearchInvoicesAsync(string? q, int skip, int take)
    {
        var query = _db.Set<Invoice>().AsNoTracking().Where(i => !i.IsCancelled);
        if (q != null)
        {
            query = query.Where(i => i.InvoiceNumber.Contains(q)
                || (i.B1InvoiceNumber != null && i.B1InvoiceNumber.Contains(q))
                || (i.CustomerPONumber != null && i.CustomerPONumber.Contains(q))
                || i.Customer.Name.Contains(q)
                || (i.Customer.CustomerCode != null && i.Customer.CustomerCode.Contains(q)));
        }
        var page = await query.OrderByDescending(i => i.Id).Skip(skip).Take(take)
            .Select(i => new { i.Id, i.InvoiceNumber, i.B1InvoiceNumber, i.Status, i.CreatedAt, i.CustomerPONumber, i.TotalAmount, Customer = i.Customer.CustomerCode ?? i.Customer.Name })
            .ToListAsync();
        return page.Select(i => new CustomerFollowUpRefOption
        {
            Id = i.Id,
            Label = i.InvoiceNumber,
            Status = i.Status,
            CreatedAt = i.CreatedAt,
            Detail = Join(i.Customer, i.B1InvoiceNumber, i.CustomerPONumber == null ? null : $"Cust. PO {i.CustomerPONumber}", Amount(i.TotalAmount)),
        }).ToList();
    }

    private async Task<List<CustomerFollowUpRefOption>> SearchFinalInvoicesAsync(string? q, int skip, int take)
    {
        var query = _db.Set<FinalInvoice>().AsNoTracking();
        if (q != null)
        {
            query = query.Where(i => i.InvoiceNumber.Contains(q)
                || (i.B1FinalInvoiceNumber != null && i.B1FinalInvoiceNumber.Contains(q))
                || i.ProformaInvoice.InvoiceNumber.Contains(q)
                || i.Customer.Name.Contains(q)
                || (i.Customer.CustomerCode != null && i.Customer.CustomerCode.Contains(q)));
        }
        var page = await query.OrderByDescending(i => i.Id).Skip(skip).Take(take)
            .Select(i => new { i.Id, i.InvoiceNumber, i.B1FinalInvoiceNumber, i.Status, i.CreatedAt, i.TotalAmount, Proforma = i.ProformaInvoice.InvoiceNumber, Customer = i.Customer.CustomerCode ?? i.Customer.Name })
            .ToListAsync();
        return page.Select(i => new CustomerFollowUpRefOption
        {
            Id = i.Id,
            Label = i.InvoiceNumber,
            Status = i.Status,
            CreatedAt = i.CreatedAt,
            Detail = Join(i.Customer, i.B1FinalInvoiceNumber, $"PI {i.Proforma}", Amount(i.TotalAmount)),
        }).ToList();
    }

    /// <summary>Document numbers of one Other Refs type, by id; ids that no longer exist are absent.</summary>
    private async Task<Dictionary<long, string>> RefLabelsAsync(string type, List<long> ids)
    {
        if (ids.Count == 0) return [];
        return type switch
        {
            CustomerFollowUpRefTypes.PurchaseOrder => await _db.Set<PurchaseOrder>().AsNoTracking()
                .Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.PONumber),
            CustomerFollowUpRefTypes.Quote => await _db.Set<Quote>().AsNoTracking()
                .Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.QuoteNumber),
            CustomerFollowUpRefTypes.Invoice => await _db.Set<Invoice>().AsNoTracking()
                .Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.InvoiceNumber),
            CustomerFollowUpRefTypes.FinalInvoice => await _db.Set<FinalInvoice>().AsNoTracking()
                .Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.InvoiceNumber),
            _ => [],
        };
    }

    private static string RefTypeLabel(string type) => type switch
    {
        CustomerFollowUpRefTypes.PurchaseOrder => "Purchase Order",
        CustomerFollowUpRefTypes.Invoice => "Invoice",
        CustomerFollowUpRefTypes.FinalInvoice => "Final Invoice",
        _ => type,
    };

    /// <summary>Other Refs per row (for <paramref name="rowIds"/>, or every row when null), in type order then as added.</summary>
    private async Task<Dictionary<long, List<CustomerFollowUpRefResponse>>> RowRefsAsync(IReadOnlyCollection<long>? rowIds)
    {
        var query = _db.Set<CustomerFollowUpRef>().AsNoTracking();
        if (rowIds != null) query = query.Where(r => rowIds.Contains(r.CustomerFollowUpId));
        var refs = await query.OrderBy(r => r.Id).ToListAsync();
        if (refs.Count == 0) return [];

        var labels = new Dictionary<string, Dictionary<long, string>>();
        foreach (var group in refs.GroupBy(r => r.RefType))
            labels[group.Key] = await RefLabelsAsync(group.Key, group.Select(r => r.RefId).Distinct().ToList());

        return refs
            .GroupBy(r => r.CustomerFollowUpId)
            .ToDictionary(g => g.Key, g => g
                .OrderBy(r => Array.IndexOf(CustomerFollowUpRefTypes.All, r.RefType))
                .Select(r =>
                {
                    var label = labels.GetValueOrDefault(r.RefType)?.GetValueOrDefault(r.RefId);
                    return new CustomerFollowUpRefResponse { Type = r.RefType, Id = r.RefId, Label = label ?? $"#{r.RefId}", Missing = label == null };
                })
                .ToList());
    }

    private async Task<Dictionary<long, CustomerFollowUpRfqRef>> RfqRefsAsync(IEnumerable<long> rfqIds)
    {
        var ids = rfqIds.Distinct().ToList();
        if (ids.Count == 0) return [];
        return await _db.Set<RFQHeader>().AsNoTracking()
            .Where(r => ids.Contains(r.Id))
            .Select(r => new CustomerFollowUpRfqRef { Id = r.Id, Name = r.Name, Status = r.Status, CustomerCode = r.Customer.CustomerCode ?? r.Customer.Name })
            .ToDictionaryAsync(r => r.Id);
    }

    /// <summary>The row with its counts, RFQ and Other Refs (unread flag left false).</summary>
    private async Task<CustomerFollowUpResponse> FullResponseAsync(CustomerFollowUp row)
    {
        var documentCount = await _db.Set<CustomerFollowUpDocument>().CountAsync(d => d.CustomerFollowUpId == row.Id);
        var messageCount = await _db.Set<CustomerFollowUpMessage>().CountAsync(m => m.CustomerFollowUpId == row.Id);
        var response = Map(row, documentCount, messageCount);
        if (row.RfqId is { } rfqId) response.Rfq = (await RfqRefsAsync([rfqId])).GetValueOrDefault(rfqId);
        response.Refs = (await RowRefsAsync([row.Id])).GetValueOrDefault(row.Id) ?? [];
        return response;
    }

    // ── Procurement users ────────────────────────────────────────────────────

    private Task<bool> IsProcurementUserAsync(string userName) =>
        _db.Set<MenuPermission>().AnyAsync(p => p.Feature == ProcurementFeature && p.UserName == userName);

    /// <summary>A procurement user working on an Open row moves its Procurement Status to In Progress (saved by the caller).</summary>
    private async Task MarkProcurementWorkAsync(CustomerFollowUp row, string userName)
    {
        if (row.ProcurementStatus == StatusOpen && await IsProcurementUserAsync(userName))
            row.ProcurementStatus = StatusInProgress;
    }

    /// <summary>Same, for actions that do not load the row themselves (documents, chat).</summary>
    private async Task MarkProcurementWorkAsync(long id, string userName)
    {
        var row = await _db.Set<CustomerFollowUp>().FirstOrDefaultAsync(f => f.Id == id);
        if (row != null) await MarkProcurementWorkAsync(row, userName);
    }

    private static void Touch(CustomerFollowUp row, string userName)
    {
        row.ModifiedBy = userName;
        row.ModifyAt = DateTime.UtcNow;
    }

    /// <summary>Open / In Progress / Done, matched loosely ("in progress", "InProgress"); anything else, or empty, is rejected.</summary>
    private static string WorkStatus(string? value, string label)
    {
        static string Squash(string s) => new(s.Where(char.IsLetter).Select(char.ToLowerInvariant).ToArray());
        var match = value == null ? null : WorkStatuses.FirstOrDefault(s => Squash(s) == Squash(value));
        return match ?? throw new ArgumentException($"{label} must be {string.Join(", ", WorkStatuses)}.");
    }

    private static string? Join(params string?[] parts) =>
        string.Join(" · ", parts.Where(p => !string.IsNullOrWhiteSpace(p))) is { Length: > 0 } text ? text : null;

    private static string? Amount(decimal? value) => value is { } v ? v.ToString("#,0.##", CultureInfo.InvariantCulture) : null;

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
        _db.Set<CustomerFollowUpRef>().RemoveRange(
            await _db.Set<CustomerFollowUpRef>().Where(r => r.CustomerFollowUpId == id).ToListAsync());
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
        await MarkProcurementWorkAsync(id, userName);
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
        await MarkProcurementWorkAsync(id, userName);
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
        Category = f.Category,
        PartNumber = f.PartNumber,
        RequestedItem = f.RequestedItem,
        DateOfRequest = f.DateOfRequest,
        Deadline = f.Deadline,
        Assign = f.Assign,
        ProcurementStatus = f.ProcurementStatus,
        SalesStatus = f.SalesStatus,
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
