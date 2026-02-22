using Microsoft.EntityFrameworkCore;
using Procument.Module.Sales.DTOs;
using Procument.Module.Sales.Entities;
using Procument.Module.Sales.Services;
using Procument.Shared.Audit;
using Procument.Module.Identity.Services;
using Procument.Module.Identity.Entities;
using Procument.Shared.Entities;

namespace Procument.Module.Sales.Services;

public class InvoiceService : IInvoiceService
{
    private readonly DbContext _db;
    private readonly IPermissionService _permissionService;

    public InvoiceService(DbContext db, IPermissionService permissionService)
    {
        _db = db;
        _permissionService = permissionService;
    }

    public async Task<PagedResult<InvoiceResponse>> GetAllAsync(int page, int pageSize, long userId, bool isAdmin)
    {
        IQueryable<Invoice> query = _db.Set<Invoice>()
            .Include(i => i.Customer)
            .Include(i => i.Quote) // Needed for owner check
            .Include(i => i.InvoiceItems);

        if (!isAdmin)
        {
            // 1. Get Permitted Invoice IDs
            var permittedInvoiceIdsStr = await _db.Set<EntityPermission>()
                .Where(p => p.UserId == userId && p.EntityName == "Invoice")
                .Select(p => p.EntityId)
                .ToListAsync();

            var permittedIds = permittedInvoiceIdsStr
                .Select(id => long.TryParse(id, out var l) ? l : -1)
                .ToList();

            // 2. Filter: Owner (via Quote) OR Assigned Permission
            query = query.Where(i => i.Quote.UserId == userId || permittedIds.Contains(i.Id));
        }

        query = query.OrderByDescending(i => i.CreatedAt);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<InvoiceResponse>
        {
            Items = items.Select(MapToResponse).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<InvoiceResponse?> GetByIdAsync(long id, long userId, bool isAdmin)
    {
        var invoice = await _db.Set<Invoice>()
            .Include(i => i.Customer)
            .Include(i => i.Quote)
            .Include(i => i.InvoiceItems)
                .ThenInclude(ii => ii.QuoteItem)
                .ThenInclude(qi => qi.PartNumber)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice == null) return null;

        if (!isAdmin && invoice.Quote.UserId != userId)
        {
            // Check specific permission
            var hasPermission = await _permissionService.HasPermissionAsync(userId, "Invoice", id.ToString(), "View")
                             || await _permissionService.HasPermissionAsync(userId, "Invoice", id.ToString(), "Edit");

            if (!hasPermission) return null;
        }

        return MapToResponse(invoice);
    }

    public async Task<InvoiceResponse> CreateAsync(CreateInvoiceRequest request, long userId)
    {
        var quote = await _db.Set<Quote>()
            .Include(q => q.QuoteItems)
            .FirstOrDefaultAsync(q => q.Id == request.QuoteId);

        if (quote == null) throw new KeyNotFoundException("Quote not found");

        // Generate Invoice #
        var count = await _db.Set<Invoice>().CountAsync() + 1;
        var invoiceNumber = $"INV-{count:D5}";

        var invoiceItems = new List<InvoiceItem>();
        decimal totalAmount = 0;

        foreach (var itemReq in request.Items)
        {
            var quoteItem = quote.QuoteItems.FirstOrDefault(qi => qi.Id == itemReq.QuoteItemId);
            if (quoteItem == null) continue;

            var totalPrice = itemReq.Qty * itemReq.UnitPrice;
            totalAmount += totalPrice;

            invoiceItems.Add(new InvoiceItem
            {
                QuoteItemId = itemReq.QuoteItemId,
                Qty = itemReq.Qty,
                UnitPrice = itemReq.UnitPrice,
                TotalPrice = totalPrice,
                ExpectedDeliveryDate = itemReq.ExpectedDeliveryDate
            });
        }

        var invoice = new Invoice
        {
            InvoiceNumber = invoiceNumber,
            QuoteId = request.QuoteId,
            CustomerId = quote.CustomerId,
            TotalAmount = totalAmount,
            Status = "Pending",
            DueDate = request.DueDate,
            CreatedAt = DateTime.UtcNow,
            InvoiceItems = invoiceItems
        };

        _db.Set<Invoice>().Add(invoice);
        await _db.SaveChangesAsync();

        return await GetByIdAsync(invoice.Id, userId, true) ?? throw new Exception("Failed to load created invoice");
    }

    public async Task<bool> UpdateStatusAsync(long id, string status, long userId, bool isAdmin)
    {
        var invoice = await _db.Set<Invoice>()
            .Include(i => i.Quote)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice == null) return false;
        if (!isAdmin && invoice.Quote.UserId != userId) return false;

        invoice.Status = status;
        if (status == "Paid" && invoice.PaidDate == null)
        {
            invoice.PaidDate = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var invoice = await _db.Set<Invoice>().FindAsync(id);
        if (invoice == null) return false;

        _db.Set<Invoice>().Remove(invoice);
        await _db.SaveChangesAsync();
        return true;
    }

    private static InvoiceResponse MapToResponse(Invoice i) => new()
    {
        Id = i.Id,
        InvoiceNumber = i.InvoiceNumber,
        TotalAmount = i.TotalAmount,
        Status = i.Status,
        DueDate = i.DueDate,
        PaidDate = i.PaidDate,
        CreatedAt = i.CreatedAt,
        QuoteId = i.QuoteId,
        CustomerId = i.CustomerId,
        CustomerName = i.Customer?.Name ?? "",
        Items = i.InvoiceItems?.Select(ii => new InvoiceItemResponse
        {
            Id = ii.Id,
            Qty = ii.Qty,
            UnitPrice = ii.UnitPrice,
            TotalPrice = ii.TotalPrice,
            ExpectedDeliveryDate = ii.ExpectedDeliveryDate,
            QuoteItemId = ii.QuoteItemId,
            PartNumberName = ii.QuoteItem?.PartNumber?.Name ?? "",
            Description = ii.QuoteItem?.PartNumber?.Description ?? ""
        }).ToList() ?? new()
    };

    public async Task<bool> GrantPermissionsAsync(List<long> invoiceIds, long targetUserId, string permission)
    {
        // 1. Validate Target User
        var targetUser = await _db.Set<User>().FindAsync(targetUserId);
        if (targetUser == null) return false;

        // 2. Validate Invoices exist
        var count = await _db.Set<Invoice>().CountAsync(i => invoiceIds.Contains(i.Id));
        if (count != invoiceIds.Count) return false; // Some not found

        // 3. Grant Permissions
        foreach (var id in invoiceIds)
        {
            await _permissionService.AddPermissionAsync(targetUserId, "Invoice", id.ToString(), permission);
        }

        return true;
    }
}
