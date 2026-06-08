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

    public async Task<PagedResult<InvoiceResponse>> GetAllAsync(PageQuery page, long userId, bool isAdmin, string? status = null, string? customer = null, string? sortBy = null, bool sortDesc = false, List<string>? customerCodes = null, List<string>? statuses = null, List<string>? invoiceNumbers = null, bool isSuperAdmin = true, int[]? userBases = null)
    {
        IQueryable<Invoice> query = _db.Set<Invoice>()
            .AsNoTracking()
            .Include(i => i.Customer)
            .Include(i => i.Quote)
            .Include(i => i.InvoiceItems);

        if (!isSuperAdmin && userBases != null)
        {
            var permittedInvoiceIdsStr = await _db.Set<EntityPermission>()
                .Where(p => p.UserId == userId && p.EntityName == "Invoice")
                .Select(p => p.EntityId)
                .ToListAsync();

            var permittedIds = permittedInvoiceIdsStr
                .Select(id => long.TryParse(id, out var l) ? l : -1)
                .ToList();

            query = query.Where(i =>
                i.Customer == null ||
                i.Customer.Base == null ||
                userBases.Contains(i.Customer.Base.Value) ||
                permittedIds.Contains(i.Id) ||
                i.Quote.UserId == userId);
        }
        else if (!isAdmin)
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

        // By default, hide cancelled invoices unless "Cancelled" is explicitly requested
        bool cancelledRequested = status == "Cancelled" || (statuses != null && statuses.Contains("Cancelled"));
        if (status == "Cancelled")
            query = query.Where(i => i.IsCancelled);
        else if (!cancelledRequested)
            query = query.Where(i => !i.IsCancelled);

        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var s = page.Search.Trim();
            query = query.Where(i => i.InvoiceNumber.Contains(s) || i.Customer.Name.Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(status) && status != "All" && status != "Cancelled")
            query = query.Where(i => i.Status == status);

        if (!string.IsNullOrWhiteSpace(customer))
            query = query.Where(i => i.Customer.Name.Contains(customer));

        if (customerCodes?.Count > 0)
        {
            var codes = customerCodes.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
            if (codes.Count > 0)
            {
                var hasNullPlaceholder = codes.Contains("-") || codes.Contains("—");
                query = query.Where(i => 
                    codes.Contains(i.Customer.CustomerCode) ||
                    (hasNullPlaceholder && (i.Customer.CustomerCode == null || i.Customer.CustomerCode == ""))
                );
            }
        }

        if (statuses?.Count > 0)
        {
            var sts = statuses.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            if (sts.Count > 0)
                query = query.Where(i => sts.Contains(i.Status));
        }

        if (invoiceNumbers?.Count > 0)
        {
            var invs = invoiceNumbers.Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
            if (invs.Count > 0)
                query = query.Where(i => invs.Contains(i.InvoiceNumber));
        }

        query = sortBy switch
        {
            "invoiceNumber" => sortDesc ? query.OrderByDescending(i => i.InvoiceNumber) : query.OrderBy(i => i.InvoiceNumber),
            "customerCode"  => sortDesc ? query.OrderByDescending(i => i.Customer != null ? i.Customer.CustomerCode : "") : query.OrderBy(i => i.Customer != null ? i.Customer.CustomerCode : ""),
            "subject"       => sortDesc ? query.OrderByDescending(i => i.Subject) : query.OrderBy(i => i.Subject),
            "totalAmount"   => sortDesc ? query.OrderByDescending(i => i.TotalAmount) : query.OrderBy(i => i.TotalAmount),
            "status"        => sortDesc ? query.OrderByDescending(i => i.Status) : query.OrderBy(i => i.Status),
            "createdAt"     => sortDesc ? query.OrderByDescending(i => i.CreatedAt) : query.OrderBy(i => i.CreatedAt),
            _               => query.OrderByDescending(i => i.CreatedAt),
        };

        var totalCount = await query.CountAsync();
        var totalAmountSum = await query.SumAsync(i => (decimal?)i.TotalAmount) ?? 0m;
        var items = await query
            .ApplyPaging(page)
            .ToListAsync();

        return new PagedResult<InvoiceResponse>
        {
            Items = items.Select(MapToResponse).ToList(),
            TotalCount = totalCount,
            TotalAmountSum = totalAmountSum,
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
            DeadlineDate = request.DeadlineDate,
            CustomerPONumber = request.CustomerPONumber,
            CustomerPODate = request.CustomerPODate,
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

            if (itemReq.ExpectedDeliveryDate.HasValue)
                item.ExpectedDeliveryDate = itemReq.ExpectedDeliveryDate;
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
        if (request.DeadlineDate.HasValue) invoice.DeadlineDate = request.DeadlineDate.Value;
        if (request.CustomerPONumber != null) invoice.CustomerPONumber = request.CustomerPONumber;
        invoice.CustomerPODate = request.CustomerPODate;
        if (request.Subject != null) invoice.Subject = request.Subject;
        if (request.Tax.HasValue) invoice.Tax = request.Tax.Value;
        if (request.Shipping.HasValue) invoice.Shipping = request.Shipping.Value;
        if (request.ProcessingFee.HasValue) invoice.ProcessingFee = request.ProcessingFee.Value;
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
                    .ThenInclude(qi => qi!.PartNumber)
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

        // ── QTY mismatch guard: block auto-finalize when invoice qty differs from quote qty ──
        if (status == "Running" && autoFinalize)
        {
            var mismatchedItems = invoice.InvoiceItems
                .Where(ii => ii.QuoteItem != null && ii.Qty != ii.QuoteItem.Qty)
                .Select(ii => ii.QuoteItem!.PartNumber?.Name ?? $"Item #{ii.Id}")
                .ToList();

            if (mismatchedItems.Count > 0)
            {
                var partList = string.Join(", ", mismatchedItems.Take(5));
                var suffix = mismatchedItems.Count > 5 ? $" (+{mismatchedItems.Count - 5} more)" : "";
                throw new InvalidOperationException(
                    $"Cannot auto-finalize: QTY was changed on {mismatchedItems.Count} item(s) " +
                    $"({partList}{suffix}) compared to the supplier quote. " +
                    "Auto-finalize cannot automatically adjust supplier quantities. " +
                    "Please choose manual finalization and update the quantities in the Procurement page.");
            }
        }

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

    /// <summary>
    /// Soft-cancel a Proforma Invoice. Sets IsCancelled = true, CancelledAt = now, Status = "Cancelled".
    /// Cascades: cancels all linked POs, Procurements, ProcurementItems, and unassigned POItems.
    /// Cannot cancel an invoice that already has a Final Invoice created from it.
    /// </summary>
    public async Task<bool> CancelAsync(long id)
    {
        var invoice = await _db.Set<Invoice>().FindAsync(id);
        if (invoice == null || invoice.IsCancelled) return false;

        invoice.IsCancelled = true;
        invoice.CancelledAt = DateTime.UtcNow;
        invoice.Status = "Cancelled";

        // ── 1. Cancel all POs linked to this invoice (skip already-terminal ones) ──
        var terminalPOStatuses = new[] { "Completed", "Returned", "Cancelled" };
        var linkedPOs = await _db.Set<PurchaseOrder>()
            .Where(po => po.InvoiceId == id && !terminalPOStatuses.Contains(po.Status))
            .ToListAsync();
        foreach (var po in linkedPOs)
            po.Status = "Cancelled";

        // ── 2. Cancel all Procurements linked to this invoice ──
        var linkedProcs = await _db.Set<Procurement>()
            .Include(p => p.Items)
            .Where(p => p.InvoiceId == id && p.Status != "Cancelled")
            .ToListAsync();
        foreach (var proc in linkedProcs)
        {
            proc.Status = "Cancelled";
            foreach (var item in proc.Items.Where(i => i.ItemStatus != "Cancelled"))
                item.ItemStatus = "Cancelled";
        }

        // ── 3. Cancel unassigned POItems that trace back to this invoice ──
        // (POItems created from finalized Procurement but not yet grouped into a PO)
        var invoiceItemIds = await _db.Set<InvoiceItem>()
            .Where(ii => ii.InvoiceId == id)
            .Select(ii => ii.Id)
            .ToListAsync();
        if (invoiceItemIds.Count > 0)
        {
            var unassignedPOItems = await _db.Set<POItem>()
                .Where(pi => pi.POId == null && pi.InvoiceItemId != null
                             && invoiceItemIds.Contains(pi.InvoiceItemId!.Value)
                             && pi.ReturnedAt == null)
                .ToListAsync();
            foreach (var pi in unassignedPOItems)
                pi.Status = "Cancelled";
        }

        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Returns prepayment check info: whether the customer's total POP (Customer Payments)
    /// meets the required PrepaymentPercent of the invoice total.
    /// </summary>
    public async Task<PrepaymentCheckResponse?> GetPrepaymentCheckAsync(long id)
    {
        var invoice = await _db.Set<Invoice>()
            .AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => new { i.TotalAmount, i.PaymentStatus, i.PrepaymentPercent })
            .FirstOrDefaultAsync();

        if (invoice == null) return null;

        var totalPaid = await _db.Set<CustomerPayment>()
            .Where(p => p.InvoiceId == id)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        var requiredAmount = invoice.PaymentStatus == "Prepayment" && invoice.PrepaymentPercent.HasValue
            ? Math.Round(invoice.TotalAmount * invoice.PrepaymentPercent.Value / 100, 2)
            : 0m;

        return new PrepaymentCheckResponse
        {
            PaymentStatus = invoice.PaymentStatus,
            PrepaymentPercent = invoice.PrepaymentPercent,
            TotalAmount = invoice.TotalAmount,
            RequiredAmount = requiredAmount,
            TotalPaid = totalPaid,
            IsSufficient = invoice.PaymentStatus != "Prepayment" || totalPaid >= requiredAmount
        };
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
            IsCancelled = i.IsCancelled,
            CancelledAt = i.CancelledAt,
            PaymentStatus = i.PaymentStatus,
            PrepaymentPercent = i.PrepaymentPercent,
            DueDate = i.DueDate,
            DeadlineDate = i.DeadlineDate,
            PaidDate = i.PaidDate,
            CreatedAt = i.CreatedAt,
            CustomerPONumber = i.CustomerPONumber,
            CustomerPODate = i.CustomerPODate,
            Subject = i.Subject,
            Tax = i.Tax,
            Shipping = i.Shipping,
            ProcessingFee = i.ProcessingFee,
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
            CustomerTermsAndConditions = !string.IsNullOrWhiteSpace(i.Customer?.PITermsAndConditions)
                ? i.Customer.PITermsAndConditions
                : i.Customer?.TermsAndConditions,
            CustomerCurrencyType = i.Customer?.CurrencyType,
            CustomerBase = i.Customer?.Base,
            RfqExType = i.InvoiceItems?
                .Select(ii => ii.QuoteItem?.RFQItem?.RFQ?.ExType)
                .FirstOrDefault(x => x.HasValue),
            DefaultDepositWalletId = i.DefaultDepositWalletId,
            QuoteCoefYuan = i.Quote?.CoefYuan,
            QuoteExchangeRateYuan = i.Quote?.ExchangeRateYuan,
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
                Alt = ii.QuoteItem?.Alt,
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
