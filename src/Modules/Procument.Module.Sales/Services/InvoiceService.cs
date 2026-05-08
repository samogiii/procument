using Microsoft.EntityFrameworkCore;
using Procument.Module.Sales.DTOs;
using Procument.Shared.DTOs;
using Procument.Module.Sales.Entities;
using Procument.Module.Sales.Services;
using Procument.Shared.Audit;
using Procument.Module.Identity.Services;
using Procument.Module.Identity.Entities;
using Procument.Shared.Entities;
using Procument.Module.Purchasing.Entities;
using Procument.Module.Purchasing.Services;
using Procument.Shared.Services;

namespace Procument.Module.Sales.Services;

public class InvoiceService : IInvoiceService
{
    private readonly DbContext _db;
    private readonly IPermissionService _permissionService;
    private readonly IDocumentStorageService _documentStorage;
    private readonly IProcurementService _procurementService;

    public InvoiceService(DbContext db, IPermissionService permissionService, IDocumentStorageService documentStorage, IProcurementService procurementService)
    {
        _db = db;
        _permissionService = permissionService;
        _documentStorage = documentStorage;
        _procurementService = procurementService;
    }

    public async Task<PagedResult<InvoiceResponse>> GetAllAsync(PageQuery page, long userId, bool isAdmin, string? status = null, string? customer = null)
    {
        IQueryable<Invoice> query = _db.Set<Invoice>()
            .Include(i => i.Customer)
            .Include(i => i.Quote)
            .Include(i => i.InvoiceItems);

        if (!isAdmin)
        {
            var permittedInvoiceIdsStr = await _db.Set<EntityPermission>()
                .Where(p => p.UserId == userId && p.EntityName == "Invoice")
                .Select(p => p.EntityId)
                .ToListAsync();

            var permittedIds = permittedInvoiceIdsStr
                .Select(id => long.TryParse(id, out var l) ? l : -1)
                .ToList();

            query = query.Where(i => i.Quote.UserId == userId || permittedIds.Contains(i.Id));
        }

        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var s = page.Search.Trim();
            query = query.Where(i => i.InvoiceNumber.Contains(s) || i.Customer.Name.Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(status) && status != "All")
            query = query.Where(i => i.Status == status);

        if (!string.IsNullOrWhiteSpace(customer))
            query = query.Where(i => i.Customer.Name.Contains(customer));

        query = query.OrderByDescending(i => i.CreatedAt);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((page.Page - 1) * page.PageSize)
            .Take(page.PageSize)
            .ToListAsync();

        return new PagedResult<InvoiceResponse>
        {
            Items = items.Select(MapToResponse).ToList(),
            TotalCount = totalCount,
            Page = page.Page,
            PageSize = page.PageSize
        };
    }

    public async Task<InvoiceResponse?> GetByIdAsync(long id, long userId, bool isAdmin)
    {
        var invoice = await _db.Set<Invoice>()
            .AsNoTrackingWithIdentityResolution()
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
        var primaryQuote = await _db.Set<Quote>()
            .FirstOrDefaultAsync(q => q.Id == request.QuoteId);

        if (primaryQuote == null) throw new KeyNotFoundException("Primary quote not found");

        var quoteItemIds = request.Items.Select(i => i.QuoteItemId).Distinct().ToList();

        var quoteItems = await _db.Set<QuoteItem>()
            .Include(qi => qi.Quote)
            .Where(qi => quoteItemIds.Contains(qi.Id))
            .ToListAsync();

        if (quoteItems.Any(qi => qi.Quote.CustomerId != primaryQuote.CustomerId))
        {
            throw new Exception("All selected quote items must belong to the same customer as the primary quote.");
        }

        var invoiceItems = new List<InvoiceItem>();
        decimal totalAmount = 0;

        foreach (var itemReq in request.Items)
        {
            var quoteItem = quoteItems.FirstOrDefault(qi => qi.Id == itemReq.QuoteItemId);
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

        var initialStatus = "Draft";
        if (request.PaymentStatus == "Prepayment" && request.PrepaymentPercent.HasValue && request.PrepaymentPercent.Value > 0)
        {
            initialStatus = "Waiting For PrePayment";
        }

        var invoice = new Invoice
        {
            InvoiceNumber = "",
            QuoteId = request.QuoteId,
            CustomerId = primaryQuote.CustomerId,
            TotalAmount = totalAmount,
            Status = initialStatus,
            PaymentStatus = request.PaymentStatus,
            PrepaymentPercent = request.PaymentStatus == "Prepayment" ? request.PrepaymentPercent : null,
            DueDate = request.DueDate,
            CustomerPONumber = request.CustomerPONumber,
            Subject = request.Subject,
            CreatedAt = DateTime.UtcNow,
            InvoiceItems = invoiceItems
        };

        _db.Set<Invoice>().Add(invoice);
        await _db.SaveChangesAsync();

        // Set InvoiceNumber to PINV-{Id} now that the Id is assigned
        invoice.InvoiceNumber = $"PI-{invoice.Id}";
        await _db.SaveChangesAsync();

        // Create the document folder for this Proforma Invoice
        try { _documentStorage.EnsureProformaInvoiceFolder(invoice.InvoiceNumber); }
        catch { /* folder creation must not fail invoice creation */ }

        return await GetByIdAsync(invoice.Id, userId, true) ?? throw new Exception("Failed to load created invoice");
    }

    public async Task<bool> UpdateItemsAsync(long id, UpdateInvoiceItemsRequest request)
    {
        var invoice = await _db.Set<Invoice>()
            .Include(i => i.InvoiceItems)
                .ThenInclude(ii => ii.QuoteItem)
            .FirstOrDefaultAsync(i => i.Id == id);
        if (invoice == null) return false;

        foreach (var itemReq in request.Items)
        {
            var item = invoice.InvoiceItems.FirstOrDefault(ii => ii.Id == itemReq.Id);
            if (item == null) continue;

            // New path: direct qty + unit-price edits. TotalPrice is recomputed as
            // Qty * UnitPrice and Discount is (OriginalUnitPrice - NewUnitPrice) * NewQty,
            // where the original unit price comes from the linked QuoteItem.
            if (itemReq.Qty.HasValue || itemReq.UnitPrice.HasValue)
            {
                var newQty = itemReq.Qty ?? item.Qty;
                var newUnitPrice = itemReq.UnitPrice ?? item.UnitPrice;
                if (newQty < 1) newQty = 1;
                if (newUnitPrice < 0) newUnitPrice = 0;

                var originalUnitPrice = item.QuoteItem?.UnitPrice ?? item.UnitPrice;

                item.Qty = newQty;
                item.UnitPrice = newUnitPrice;
                item.TotalPrice = newQty * newUnitPrice;

                item.Discount = (originalUnitPrice - newUnitPrice) * newQty;
            }
            else if (itemReq.FinalPrice.HasValue)
            {
                // Legacy path: user edits the row's Final Total directly.
                item.Discount = item.TotalPrice - itemReq.FinalPrice.Value;
            }
            else
            {
                item.Discount = null;
            }
        }

        // Recalculate invoice total as sum of final prices
        invoice.TotalAmount = invoice.InvoiceItems.Sum(ii => ii.TotalPrice);

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateAsync(long id, UpdateInvoiceRequest request)
    {
        var invoice = await _db.Set<Invoice>().FindAsync(id);
        if (invoice == null) return false;

        if (request.DueDate.HasValue) invoice.DueDate = request.DueDate.Value;
        if (request.CustomerPONumber != null) invoice.CustomerPONumber = request.CustomerPONumber;
        if (request.Subject != null) invoice.Subject = request.Subject;
        if (request.PaymentStatus != null)
        {
            invoice.PaymentStatus = request.PaymentStatus;
            invoice.PrepaymentPercent = request.PaymentStatus == "Prepayment" ? request.PrepaymentPercent : null;

            // Auto-advance to Waiting For PrePayment if still in Draft and we have a prepayment percentage
            if (invoice.Status == "Draft" && invoice.PaymentStatus == "Prepayment" && invoice.PrepaymentPercent.HasValue && invoice.PrepaymentPercent.Value > 0)
            {
                invoice.Status = "Waiting For PrePayment";
            }
        }

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateStatusAsync(long id, string status, long userId, bool isAdmin, bool autoFinalize = false)
    {
        var invoice = await _db.Set<Invoice>()
            .Include(i => i.Quote)
            .Include(i => i.InvoiceItems)
                .ThenInclude(ii => ii.QuoteItem)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice == null) return false;
        if (!isAdmin && invoice.Quote.UserId != userId) return false;

        // Allowed invoice workflow statuses
        var allowedStatuses = new[]
        {
            "Draft", "Pending", "Running",
            "Waiting For PrePayment", "Delivered", "Finish"
        };
        if (!allowedStatuses.Contains(status)) return false;

        // Only admin can move past Pending
        var adminOnlyStatuses = new[] { "Running", "Waiting For PrePayment", "Delivered", "Finish" };
        if (adminOnlyStatuses.Contains(status) && !isAdmin) return false;

        invoice.Status = status;

        // Stamp PaidDate when reaching the terminal Finish state
        if (status == "Finish" && invoice.PaidDate == null)
            invoice.PaidDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        // Spin up the Procurement layer when admin accepts the invoice (Running).
        // Idempotent — safe to call even if a Procurement already exists for this invoice.
        if (status == "Running")
        {
            try
            {
                await _procurementService.CreateFromAcceptedInvoiceAsync(invoice.Id, userId, autoFinalize);
            }
            catch
            {
                // Non-fatal — swallow to preserve the status-change contract.
            }
        }

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
        // Build rank map from the full ordered RFQ item list (same logic as QuoteService).
        // Null-safe: on the list endpoint RFQ.RFQItems is NOT eagerly loaded, so guard the
        // nested collection access. If items aren't loaded we just skip rank-building.
        var rfqItemRank = i.InvoiceItems?
            .Select(ii => ii.QuoteItem?.RFQItem?.RFQ)
            .Where(r => r != null && r!.RFQItems != null)
            .SelectMany(r => r!.RFQItems!)
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
            PaymentStatus = i.PaymentStatus,
            PrepaymentPercent = i.PrepaymentPercent,
            DueDate = i.DueDate,
            PaidDate = i.PaidDate,
            CreatedAt = i.CreatedAt,
            CustomerPONumber = i.CustomerPONumber,
            Subject = i.Subject,
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
            CustomerTermsAndConditions = i.Customer?.TermsAndConditions,
            CustomerCurrencyType = i.Customer?.CurrencyType,
            RfqExType = i.InvoiceItems?
                .Select(ii => ii.QuoteItem?.RFQItem?.RFQ?.ExType)
                .FirstOrDefault(x => x.HasValue),
            Items = i.InvoiceItems?.Select(ii => new InvoiceItemResponse
            {
                Id = ii.Id,
                Qty = ii.Qty,
                UnitPrice = ii.UnitPrice,
                TotalPrice = ii.TotalPrice,
                Discount = ii.Discount,
                OriginalUnitPrice = ii.QuoteItem?.UnitPrice,
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
