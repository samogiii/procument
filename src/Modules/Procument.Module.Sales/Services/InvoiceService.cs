using Microsoft.EntityFrameworkCore;
using Procument.Module.Sales.DTOs;
using Procument.Module.Sales.Entities;
using Procument.Module.Sales.Services;
using Procument.Shared.Audit;
using Procument.Module.Identity.Services;
using Procument.Module.Identity.Entities;
using Procument.Shared.Entities;
using Procument.Module.Purchasing.Entities;

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
                    .ThenInclude(qi => qi!.PartNumber)
            .Include(i => i.InvoiceItems)
                .ThenInclude(ii => ii.QuoteItem)
                    .ThenInclude(qi => qi!.ProcumentRecord)
            .Include(i => i.InvoiceItems)
                .ThenInclude(ii => ii.QuoteItem)
                    .ThenInclude(qi => qi!.RFQItem)
                        .ThenInclude(ri => ri!.RFQ)
                            .ThenInclude(r => r!.RFQItems)
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
            InvoiceNumber = "",
            QuoteId = request.QuoteId,
            CustomerId = quote.CustomerId,
            TotalAmount = totalAmount,
            Status = "Pending",
            DueDate = request.DueDate,
            CustomerPONumber = request.CustomerPONumber,
            CreatedAt = DateTime.UtcNow,
            InvoiceItems = invoiceItems
        };

        _db.Set<Invoice>().Add(invoice);
        await _db.SaveChangesAsync();

        // Set InvoiceNumber to INV-{Id} now that the Id is assigned
        invoice.InvoiceNumber = $"INV-{invoice.Id}";
        await _db.SaveChangesAsync();

        return await GetByIdAsync(invoice.Id, userId, true) ?? throw new Exception("Failed to load created invoice");
    }

    public async Task<bool> UpdateAsync(long id, UpdateInvoiceRequest request)
    {
        var invoice = await _db.Set<Invoice>().FindAsync(id);
        if (invoice == null) return false;

        if (request.DueDate.HasValue) invoice.DueDate = request.DueDate.Value;
        if (request.CustomerPONumber != null) invoice.CustomerPONumber = request.CustomerPONumber;

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateStatusAsync(long id, string status, long userId, bool isAdmin, string? rejectionNote = null)
    {
        var invoice = await _db.Set<Invoice>()
            .Include(i => i.Quote)
            .Include(i => i.InvoiceItems)
                .ThenInclude(ii => ii.QuoteItem)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice == null) return false;
        if (!isAdmin && invoice.Quote.UserId != userId) return false;

        // Only admin can change to Paid, Rejected, or Overdue
        if ((status == "Paid" || status == "Rejected" || status == "Overdue") && !isAdmin) return false;

        invoice.Status = status;
        if (status == "Paid" && invoice.PaidDate == null)
        {
            invoice.PaidDate = DateTime.UtcNow;
        }

        if (status == "Rejected")
        {
            invoice.RejectionNote = rejectionNote;
        }
        else
        {
            invoice.RejectionNote = null;
        }

        // When Proforma Invoice is Paid, auto-create POItems (without PO)
        if (status == "Paid")
        {
            foreach (var ii in invoice.InvoiceItems)
            {
                // Check if a POItem already exists for this InvoiceItem
                var exists = await _db.Set<POItem>().AnyAsync(p => p.InvoiceItemId == ii.Id);
                if (exists) continue;

                var quoteItem = ii.QuoteItem;
                var procumentRecordId = quoteItem?.ProcumentRecordId;

                // Get supplier and cost price from procurement record if available
                ProcumentRecord? proc = null;
                long? supplierId = null;
                if (procumentRecordId.HasValue)
                {
                    proc = await _db.Set<ProcumentRecord>().FindAsync(procumentRecordId.Value);
                    supplierId = proc?.SupplierId;
                }

                // Use supplier's buy price from ProcumentRecord, fallback to invoice price
                var costUnitPrice = (proc?.Price > 0)
                    ? (decimal)proc.Price
                    : ii.UnitPrice;

                var poItem = new POItem
                {
                    POId = null, // No PO assigned yet
                    InvoiceItemId = ii.Id,
                    ProcumentId = procumentRecordId,
                    PartNumberId = quoteItem?.PartNumberId,
                    SupplierId = supplierId,
                    Qty = ii.Qty,
                    UnitPrice = costUnitPrice,
                    TotalPrice = ii.Qty * costUnitPrice,
                    Condition = quoteItem?.Condition,
                };

                _db.Set<POItem>().Add(poItem);
            }
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

    private static InvoiceResponse MapToResponse(Invoice i)
    {
        // Build rank map from the full ordered RFQ item list (same logic as QuoteService)
        var rfqItemRank = i.InvoiceItems?
            .Select(ii => ii.QuoteItem?.RFQItem?.RFQ)
            .Where(r => r != null)
            .SelectMany(r => r!.RFQItems)
            .DistinctBy(ri => ri.Id)
            .OrderBy(ri => ri.Id)
            .Select((ri, idx) => new { ri.Id, rank = idx + 1 })
            .ToDictionary(x => x.Id, x => x.rank) ?? new();

        return new()
        {
            Id = i.Id,
            InvoiceNumber = i.InvoiceNumber,
            TotalAmount = i.TotalAmount,
            Status = i.Status,
            DueDate = i.DueDate,
            PaidDate = i.PaidDate,
            CreatedAt = i.CreatedAt,
            CustomerPONumber = i.CustomerPONumber,
            QuoteId = i.QuoteId,
            CustomerId = i.CustomerId,
            CustomerName = i.Customer?.Name ?? "",
            CustomerCode = i.Customer?.CustomerCode,
            CustomerContactPerson = i.Customer?.ContactPerson,
            CustomerEmail = i.Customer?.Email,
            CustomerPhone = i.Customer?.Phone,
            CustomerBillTo = i.Customer?.BillTo,
            CustomerShipTo = i.Customer?.ShipTo,
            CustomerShippingAccount = i.Customer?.ShippingAccount,
            RejectionNote = i.RejectionNote,
            Items = i.InvoiceItems?.Select(ii => new InvoiceItemResponse
            {
                Id = ii.Id,
                Qty = ii.Qty,
                UnitPrice = ii.UnitPrice,
                TotalPrice = ii.TotalPrice,
                ExpectedDeliveryDate = ii.ExpectedDeliveryDate,
                QuoteItemId = ii.QuoteItemId,
                RFQReference = ii.QuoteItem?.RFQItemId.HasValue == true &&
                               rfqItemRank.TryGetValue(ii.QuoteItem.RFQItemId!.Value, out var rank)
                               ? rank.ToString() : null,
                PartNumberName = ii.QuoteItem?.PartNumber?.Name ?? "",
                Description = ii.QuoteItem?.PartNumber?.Description ?? "",
                Condition = ii.QuoteItem?.Condition,
                CertName = ii.QuoteItem?.ProcumentRecord?.CertName,
                LeadTime = ii.QuoteItem?.ProcumentRecord?.LeadTime
            }).ToList() ?? new()
        };
    }

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
