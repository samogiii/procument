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
using Procument.Module.Catalog.Entities;

namespace Procument.Module.Sales.Services;

public class InvoiceService : IInvoiceService
{
    private readonly DbContext _db;
    private readonly IPermissionService _permissionService;
    private readonly IDocumentStorageService _documentStorage;
    private readonly IProcurementService _procurementService;
    private readonly IB1NumberService _b1Service;
    private readonly IStockReservationService _stock;

    public InvoiceService(DbContext db, IPermissionService permissionService, IDocumentStorageService documentStorage, IProcurementService procurementService, IB1NumberService b1Service, IStockReservationService stock)
    {
        _stock = stock;
        _db = db;
        _permissionService = permissionService;
        _documentStorage = documentStorage;
        _procurementService = procurementService;
        _b1Service = b1Service;
    }

    public async Task<PagedResult<InvoiceResponse>> GetAllAsync(PageQuery page, long userId, bool isAdmin, string? status = null, string? customer = null, string? sortBy = null, bool sortDesc = false, List<string>? customerCodes = null, List<string>? statuses = null, List<string>? invoiceNumbers = null, bool isSuperAdmin = true, int[]? userBases = null, string? pnSearch = null, DateTime? createdFrom = null, DateTime? createdTo = null, List<string>? subjects = null, List<int>? bases = null, List<string>? assignedUsers = null)
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
            var permittedItemIds = (await _db.Set<EntityPermission>()
                .Where(p => p.UserId == userId && p.EntityName == "InvoiceItem")
                .Select(p => p.EntityId).ToListAsync())
                .Select(id => long.TryParse(id, out var l) ? l : -1)
                .ToList();
            var sourcePermissions = await _db.Set<EntityPermission>()
                .Where(p => p.UserId == userId && (p.EntityName == "Quote" || p.EntityName == "RFQ"))
                .Select(p => new { p.EntityName, p.EntityId })
                .ToListAsync();
            var permittedQuoteIds = sourcePermissions.Where(p => p.EntityName == "Quote")
                .Select(p => long.TryParse(p.EntityId, out var id) ? id : -1).Where(id => id > 0).ToList();
            var permittedRfqIds = sourcePermissions.Where(p => p.EntityName == "RFQ")
                .Select(p => long.TryParse(p.EntityId, out var id) ? id : -1).Where(id => id > 0).ToList();

            query = query.Where(i =>
                i.Customer == null ||
                i.Customer.Base == null ||
                userBases.Contains(i.Customer.Base.Value) ||
                permittedIds.Contains(i.Id) ||
                i.InvoiceItems.Any(item => permittedItemIds.Contains(item.Id)
                    || (item.QuoteItem != null && (item.QuoteItem.Quote.UserId == userId
                        || item.QuoteItem.Quote.RFQ.UserId == userId
                        || permittedQuoteIds.Contains(item.QuoteItem.QuoteId)
                        || permittedRfqIds.Contains(item.QuoteItem.Quote.RFQId)))));
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
            var permittedItemIds = (await _db.Set<EntityPermission>()
                .Where(p => p.UserId == userId && p.EntityName == "InvoiceItem")
                .Select(p => p.EntityId).ToListAsync())
                .Select(id => long.TryParse(id, out var l) ? l : -1)
                .ToList();
            var sourcePermissions = await _db.Set<EntityPermission>()
                .Where(p => p.UserId == userId && (p.EntityName == "Quote" || p.EntityName == "RFQ"))
                .Select(p => new { p.EntityName, p.EntityId })
                .ToListAsync();
            var permittedQuoteIds = sourcePermissions.Where(p => p.EntityName == "Quote")
                .Select(p => long.TryParse(p.EntityId, out var id) ? id : -1).Where(id => id > 0).ToList();
            var permittedRfqIds = sourcePermissions.Where(p => p.EntityName == "RFQ")
                .Select(p => long.TryParse(p.EntityId, out var id) ? id : -1).Where(id => id > 0).ToList();

            query = query.Where(i => permittedIds.Contains(i.Id)
                || i.InvoiceItems.Any(item => permittedItemIds.Contains(item.Id)
                    || (item.QuoteItem != null && (item.QuoteItem.Quote.UserId == userId
                        || item.QuoteItem.Quote.RFQ.UserId == userId
                        || permittedQuoteIds.Contains(item.QuoteItem.QuoteId)
                        || permittedRfqIds.Contains(item.QuoteItem.Quote.RFQId)))));
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
            query = query.Where(i =>
                i.InvoiceNumber.Contains(s) ||
                (i.B1InvoiceNumber != null && i.B1InvoiceNumber.Contains(s)) ||
                i.Customer.Name.Contains(s) ||
                (i.Customer.CustomerCode != null && i.Customer.CustomerCode.Contains(s)) ||
                (i.Subject != null && i.Subject.Contains(s)) ||
                (i.CustomerPONumber != null && i.CustomerPONumber.Contains(s)) ||
                i.Status.Contains(s) ||
                i.InvoiceItems.Any(ii => ii.QuoteItem != null && (
                    (ii.QuoteItem.PartNumber != null && ii.QuoteItem.PartNumber.Name.Contains(s)) ||
                    (ii.QuoteItem.PartNumber != null && ii.QuoteItem.PartNumber.Description != null && ii.QuoteItem.PartNumber.Description.Contains(s)) ||
                    (ii.QuoteItem.Alt != null && ii.QuoteItem.Alt.Contains(s)))));
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

        if (subjects?.Count > 0)
        {
            var subs = subjects.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            if (subs.Count > 0)
                query = query.Where(i => i.Subject != null && subs.Contains(i.Subject));
        }

        // Base of the invoice's customer — every invoice belongs to exactly one base.
        if (bases?.Count > 0)
            query = query.Where(i => i.Customer != null && i.Customer.Base != null && bases.Contains(i.Customer.Base.Value));

        if (assignedUsers?.Count > 0)
        {
            var selectedUserIds = await _db.Set<User>()
                .Where(user => assignedUsers.Contains(user.Name))
                .Select(user => user.Id)
                .ToListAsync();
            if (selectedUserIds.Count == 0)
            {
                query = query.Where(_ => false);
            }
            else
            {
                query = query.Where(invoice =>
                    _db.Set<EntityPermission>().Any(permission => permission.EntityName == "Invoice"
                        && permission.EntityId == invoice.Id.ToString()
                        && selectedUserIds.Contains(permission.UserId))
                    || (!_db.Set<EntityPermission>().Any(permission => permission.EntityName == "Invoice"
                            && permission.EntityId == invoice.Id.ToString())
                        && invoice.InvoiceItems.Any(item => item.QuoteItem != null
                            && (selectedUserIds.Contains(item.QuoteItem.Quote.UserId)
                                || (item.QuoteItem.Quote.RFQ.UserId.HasValue && selectedUserIds.Contains(item.QuoteItem.Quote.RFQ.UserId.Value))
                                || _db.Set<EntityPermission>().Any(permission => selectedUserIds.Contains(permission.UserId)
                                    && ((permission.EntityName == "Quote" && permission.EntityId == item.QuoteItem.QuoteId.ToString())
                                        || (permission.EntityName == "RFQ" && permission.EntityId == item.QuoteItem.Quote.RFQId.ToString())))))));
            }
        }

        if (!string.IsNullOrWhiteSpace(pnSearch))
        {
            var s = pnSearch.Trim();
            query = query.Where(i => i.InvoiceItems.Any(ii =>
                (ii.QuoteItem != null && ii.QuoteItem.PartNumber != null && ii.QuoteItem.PartNumber.Name.Contains(s)) ||
                (ii.QuoteItem != null && ii.QuoteItem.Alt != null && ii.QuoteItem.Alt.Contains(s))
            ));
        }

        if (createdFrom.HasValue)
            query = query.Where(i => i.CreatedAt >= createdFrom.Value);

        if (createdTo.HasValue)
            query = query.Where(i => i.CreatedAt <= createdTo.Value.AddDays(1).AddTicks(-1));

        query = sortBy switch
        {
            "invoiceNumber"  => sortDesc ? query.OrderByDescending(i => i.InvoiceNumber) : query.OrderBy(i => i.InvoiceNumber),
            "customerCode"   => sortDesc ? query.OrderByDescending(i => i.Customer != null ? i.Customer.CustomerCode : "") : query.OrderBy(i => i.Customer != null ? i.Customer.CustomerCode : ""),
            "customerBase"   => sortDesc ? query.OrderByDescending(i => i.Customer != null ? i.Customer.Base : null) : query.OrderBy(i => i.Customer != null ? i.Customer.Base : null),
            "subject"        => sortDesc ? query.OrderByDescending(i => i.Subject) : query.OrderBy(i => i.Subject),
            "totalAmount"    => sortDesc ? query.OrderByDescending(i => i.TotalAmount) : query.OrderBy(i => i.TotalAmount),
            "status"         => sortDesc ? query.OrderByDescending(i => i.Status) : query.OrderBy(i => i.Status),
            "createdAt"      => sortDesc ? query.OrderByDescending(i => i.CreatedAt) : query.OrderBy(i => i.CreatedAt),
            "customerPODate" => sortDesc ? query.OrderByDescending(i => i.CustomerPODate) : query.OrderBy(i => i.CustomerPODate),
            "deadlineDate"   => sortDesc ? query.OrderByDescending(i => i.DeadlineDate) : query.OrderBy(i => i.DeadlineDate),
            _                => query.OrderByDescending(i => i.CreatedAt),
        };

        var totalCount = await query.CountAsync();
        var totalAmountSum = await query.SumAsync(i => (decimal?)i.TotalAmount) ?? 0m;
        var items = await query
            .ApplyPaging(page)
            .ToListAsync();
        var itemIds = items.Select(i => i.Id).ToList();
        var paidByInvoice = itemIds.Count == 0 ? new Dictionary<long, decimal>() : await _db.Set<CustomerPayment>()
            .Where(p => itemIds.Contains(p.InvoiceId))
            .GroupBy(p => p.InvoiceId)
            .Select(g => new { InvoiceId = g.Key, Total = g.Sum(p => p.Amount) })
            .ToDictionaryAsync(x => x.InvoiceId, x => x.Total);

        var responses = items.Select(i => MapToResponse(i, paidByInvoice.GetValueOrDefault(i.Id))).ToList();
        await PopulateAssignmentsAsync(responses, includeItems: false);

        return new PagedResult<InvoiceResponse>
        {
            Items = responses,
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

            if (!hasPermission)
            {
                var invoiceItemPermissionIds = invoice.InvoiceItems.Select(item => item.Id.ToString()).ToList();
                hasPermission = await _db.Set<EntityPermission>().AnyAsync(permission => permission.UserId == userId
                    && permission.EntityName == "InvoiceItem" && invoiceItemPermissionIds.Contains(permission.EntityId));
            }

            if (!hasPermission)
            {
                var sourceAccess = await _db.Set<InvoiceItem>()
                    .Where(item => item.InvoiceId == id && item.QuoteItem != null)
                    .Select(item => new
                    {
                        item.QuoteItem!.QuoteId,
                        QuoteOwnerId = item.QuoteItem.Quote.UserId,
                        RfqId = item.QuoteItem.Quote.RFQId,
                        RfqOwnerId = item.QuoteItem.Quote.RFQ.UserId,
                    })
                    .ToListAsync();
                var quoteIds = sourceAccess.Select(source => source.QuoteId.ToString()).ToList();
                var rfqIds = sourceAccess.Select(source => source.RfqId.ToString()).ToList();
                hasPermission = sourceAccess.Any(source => source.QuoteOwnerId == userId || source.RfqOwnerId == userId)
                    || await _db.Set<EntityPermission>().AnyAsync(permission => permission.UserId == userId
                        && ((permission.EntityName == "Quote" && quoteIds.Contains(permission.EntityId))
                            || (permission.EntityName == "RFQ" && rfqIds.Contains(permission.EntityId))));
            }

            if (!hasPermission) return null;
        }

        PaymentBox? wallet = invoice.DefaultDepositWalletId.HasValue
            ? await _db.Set<PaymentBox>().AsNoTracking().FirstOrDefaultAsync(b => b.Id == invoice.DefaultDepositWalletId.Value)
            : null;

        CompanyPresetBankAccount? bankAccount = invoice.DefaultBankAccountId.HasValue
            ? await _db.Set<CompanyPresetBankAccount>().AsNoTracking().FirstOrDefaultAsync(b => b.Id == invoice.DefaultBankAccountId.Value)
            : null;

        var totalPaid = await _db.Set<CustomerPayment>().Where(p => p.InvoiceId == invoice.Id).SumAsync(p => (decimal?)p.Amount) ?? 0;
        var invoiceItemIds = invoice.InvoiceItems.Select(item => item.Id).ToList();
        List<POItem> inShopItems = invoiceItemIds.Count == 0
            ? []
            : await _db.Set<POItem>()
                .AsNoTracking()
                .Where(item => item.InvoiceItemId.HasValue
                    && invoiceItemIds.Contains(item.InvoiceItemId.Value)
                    && item.ReturnedAt == null
                    && item.Status == PurchaseOrderStatusFlow.InShop
                    && (!item.POId.HasValue || (item.PurchaseOrder != null
                        && item.PurchaseOrder.Status != PurchaseOrderStatusFlow.Cancelled
                        && item.PurchaseOrder.Status != PurchaseOrderStatusFlow.Returned)))
                .ToListAsync();
        var itemDisplayStatuses = inShopItems
            .GroupBy(item => item.InvoiceItemId!.Value)
            .ToDictionary(
                group => group.Key,
                group => PurchaseOrderStatusFlow.DisplayStatus(group
                    .OrderByDescending(item => item.InShopStartedAt)
                    .ThenByDescending(item => item.Id)
                    .First()));

        var response = MapToResponse(invoice, totalPaid, itemDisplayStatuses);
        await PopulateAssignmentsAsync([response], includeItems: true);
        if (wallet != null)
        {
            response.WalletBankName = wallet.BankName;
            response.WalletBankAddress = wallet.BankAddress;
            response.WalletAccountNumber = wallet.AccountNumber;
            response.WalletBeneficiaryName = wallet.BeneficiaryName;
            response.WalletSwiftCode = wallet.SwiftCode;
        }
        if (bankAccount != null)
        {
            response.BankAccountName = bankAccount.AccountName;
            response.BankAccountBankName = bankAccount.BankName;
            response.BankAccountBankAddress = bankAccount.BankAddress;
            response.BankAccountNumber = bankAccount.AccountNumber;
            response.BankAccountBeneficiaryName = bankAccount.BeneficiaryName;
            response.BankAccountSwiftCode = bankAccount.SwiftCode;
        }
        return response;
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
                Condition = quoteItem.Condition,
                ExpectedDeliveryDate = itemReq.ExpectedDeliveryDate,
                Status = "Not Started"
            });
        }

        var initialStatus = "Draft";
        var paymentTerm = InvoicePaymentTerms.Normalize(request.PaymentStatus, request.PaymentTermDays);
        if (paymentTerm.Term == InvoicePaymentTerms.Credit)
            await InvoicePaymentTerms.EnsureCreditAvailableAsync(_db, primaryQuote.CustomerId, totalAmount);

        var invoice = new Invoice
        {
            InvoiceNumber = "",
            // Same digits as the source quote's B1 number, re-lettered P (null when the quote has none).
            B1InvoiceNumber = _b1Service.Relabel(primaryQuote.B1QuoteNumber, B1NumberService.InvoiceLetter),
            QuoteId = request.QuoteId,
            CustomerId = primaryQuote.CustomerId,
            TotalAmount = totalAmount,
            Status = initialStatus,
            PaymentStatus = paymentTerm.Term,
            PaymentTermDays = paymentTerm.Days,
            PrepaymentPercent = paymentTerm.Term == InvoicePaymentTerms.Prepayment ? request.PrepaymentPercent : null,
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

        // Seed the PI with everyone who worked on its source Quotes/RFQs. Item rows
        // inherit this list until an admin gives that item its own assignment.
        var sourceQuotes = quoteItems.Select(item => item.Quote).DistinctBy(quote => quote.Id).ToList();
        var sourceRfqIds = sourceQuotes.Select(quote => quote.RFQId).Distinct().ToList();
        var sourceQuoteIdStrings = sourceQuotes.Select(quote => quote.Id.ToString()).ToList();
        var sourceRfqIdStrings = sourceRfqIds.Select(rfqId => rfqId.ToString()).ToList();
        var sourceUsers = sourceQuotes.Select(quote => quote.UserId).ToHashSet();
        var rfqOwners = await _db.Set<Procument.Module.RFQ.Entities.RFQHeader>()
            .Where(rfq => sourceRfqIds.Contains(rfq.Id) && rfq.UserId.HasValue)
            .Select(rfq => rfq.UserId!.Value)
            .ToListAsync();
        sourceUsers.UnionWith(rfqOwners);
        var sourcePermissions = await _db.Set<EntityPermission>()
            .Where(permission =>
                (permission.EntityName == "Quote" && sourceQuoteIdStrings.Contains(permission.EntityId))
                || (permission.EntityName == "RFQ" && sourceRfqIdStrings.Contains(permission.EntityId)))
            .Select(permission => permission.UserId)
            .ToListAsync();
        sourceUsers.UnionWith(sourcePermissions);
        foreach (var assignedUserId in sourceUsers)
        {
            _db.Set<EntityPermission>().Add(new EntityPermission
            {
                UserId = assignedUserId,
                EntityName = "Invoice",
                EntityId = invoice.Id.ToString(),
                Permission = "Edit",
                CreatedAt = DateTime.UtcNow,
            });
        }
        if (sourceUsers.Count > 0) await _db.SaveChangesAsync();

        // Create the document folder for this Proforma Invoice
        try { _documentStorage.EnsureProformaInvoiceFolder(invoice.InvoiceNumber); }
        catch { /* folder creation must not fail invoice creation */ }

        return await GetByIdAsync(invoice.Id, userId, true) ?? throw new Exception("Failed to load created invoice");
    }

    public async Task<bool> UpdateItemsAsync(long id, UpdateInvoiceItemsRequest request, long userId, bool isAdmin)
    {
        var invoice = await _db.Set<Invoice>()
            .Include(i => i.InvoiceItems)
                .ThenInclude(ii => ii.QuoteItem)
            .Include(i => i.Quote)
                .ThenInclude(quote => quote.RFQ)
            .FirstOrDefaultAsync(i => i.Id == id);
        if (invoice == null) return false;
        if (!isAdmin && !await CanEditItemsAsync(invoice, userId)) return false;

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

            item.Condition = string.IsNullOrWhiteSpace(itemReq.Condition)
                ? null
                : itemReq.Condition.Trim();
        }

        // Recalculate invoice total as sum of final prices
        var newTotal = invoice.InvoiceItems.Sum(ii => ii.TotalPrice);
        if (invoice.PaymentStatus == InvoicePaymentTerms.Credit)
            await InvoicePaymentTerms.EnsureCreditAvailableAsync(_db, invoice.CustomerId, newTotal, invoice.Id);
        invoice.TotalAmount = newTotal;

        // Final Invoices are kept live with their source PI. Only rows included in
        // this request are changed; tracking/certificate fields remain untouched.
        var changedItemIds = request.Items.Select(item => item.Id).Distinct().ToList();
        var finalItems = await _db.Set<FinalInvoiceItem>()
            .Where(item => item.InvoiceItemId.HasValue && changedItemIds.Contains(item.InvoiceItemId.Value))
            .ToListAsync();
        var invoiceItemsById = invoice.InvoiceItems.ToDictionary(item => item.Id);
        foreach (var finalItem in finalItems)
        {
            if (!finalItem.InvoiceItemId.HasValue
                || !invoiceItemsById.TryGetValue(finalItem.InvoiceItemId.Value, out var sourceItem)) continue;
            finalItem.Qty = sourceItem.Qty;
            finalItem.UnitPrice = sourceItem.UnitPrice;
            finalItem.TotalPrice = sourceItem.TotalPrice;
            finalItem.Discount = sourceItem.Discount;
            finalItem.Condition = sourceItem.Condition ?? sourceItem.QuoteItem?.Condition;
        }

        var finalInvoices = await _db.Set<FinalInvoice>()
            .Where(finalInvoice => finalInvoice.ProformaInvoiceId == id)
            .ToListAsync();
        foreach (var finalInvoice in finalInvoices)
            finalInvoice.TotalAmount = newTotal;

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<RemoveInvoiceItemResponse?> RemoveItemAsync(long id, long itemId, long userId, bool isAdmin)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<RemoveInvoiceItemResponse?>(async () =>
        {
            _db.ChangeTracker.Clear();
            return await RemoveItemInTransactionAsync(id, itemId, userId, isAdmin);
        });
    }

    private async Task<RemoveInvoiceItemResponse?> RemoveItemInTransactionAsync(long id, long itemId, long userId, bool isAdmin)
    {
        var invoice = await _db.Set<Invoice>()
            .Include(i => i.InvoiceItems)
            .Include(i => i.Quote)
                .ThenInclude(quote => quote.RFQ)
            .FirstOrDefaultAsync(i => i.Id == id);
        var item = invoice?.InvoiceItems.FirstOrDefault(invoiceItem => invoiceItem.Id == itemId);
        if (invoice == null || item == null) return null;
        if (!isAdmin && !await CanEditItemsAsync(invoice, userId)) return null;

        await using var transaction = await _db.Database.BeginTransactionAsync();
        var now = DateTime.UtcNow;

        var linkedPoItems = await _db.Set<POItem>()
            .Where(poItem => poItem.InvoiceItemId == itemId && poItem.ReturnedAt == null)
            .ToListAsync();
        var linkedPoIds = linkedPoItems
            .Where(poItem => poItem.POId.HasValue)
            .Select(poItem => poItem.POId!.Value)
            .Distinct()
            .ToList();
        var linkedPos = linkedPoIds.Count == 0
            ? []
            : await _db.Set<PurchaseOrder>().Where(po => linkedPoIds.Contains(po.Id)).ToListAsync();

        // Removing one ordered PI line cancels its containing PO as requested. All
        // live lines on that PO are retired together so no partial ghost PO remains.
        var allPoItemsToRetire = linkedPoIds.Count == 0
            ? linkedPoItems
            : await _db.Set<POItem>()
                .Where(poItem => poItem.ReturnedAt == null
                    && (poItem.InvoiceItemId == itemId
                        || (poItem.POId.HasValue && linkedPoIds.Contains(poItem.POId.Value))
                        || (poItem.POId == null && poItem.ReturnedFromPOId.HasValue
                            && linkedPoIds.Contains(poItem.ReturnedFromPOId.Value))))
                .ToListAsync();
        foreach (var poItem in allPoItemsToRetire)
        {
            poItem.Status = PurchaseOrderStatusFlow.Cancelled;
            poItem.ReturnedAt = now;
            if (poItem.InvoiceItemId == itemId) poItem.InvoiceItemId = null;
        }
        foreach (var po in linkedPos)
            po.Status = PurchaseOrderStatusFlow.Cancelled;

        if (linkedPoIds.Count > 0)
        {
            var linkedProcurements = await _db.Set<Procurement>()
                .Where(procurement => procurement.InvoiceId == id)
                .ToListAsync();
            foreach (var procurement in linkedProcurements)
                procurement.Status = "Open";
        }

        var otherAffectedInvoiceItemIds = allPoItemsToRetire
            .Where(poItem => poItem.InvoiceItemId.HasValue && poItem.InvoiceItemId.Value != itemId)
            .Select(poItem => poItem.InvoiceItemId!.Value)
            .Distinct()
            .ToList();
        if (otherAffectedInvoiceItemIds.Count > 0)
        {
            var otherInvoiceItems = await _db.Set<InvoiceItem>()
                .Where(invoiceItem => otherAffectedInvoiceItemIds.Contains(invoiceItem.Id))
                .ToListAsync();
            foreach (var otherInvoiceItem in otherInvoiceItems)
                otherInvoiceItem.Status = PurchaseOrderStatusFlow.Cancelled;
        }

        var procurementItems = await _db.Set<ProcurementItem>()
            .Where(procurementItem => procurementItem.SourceInvoiceItemId == itemId)
            .ToListAsync();
        foreach (var procurementItem in procurementItems)
            procurementItem.ItemStatus = "Cancelled";
        // A removed Sales Order line gives back any Our Stock it was holding.
        if (procurementItems.Any(p => p.FromStock)) await _stock.ReleaseAsync(itemId, userId);

        var finalItems = await _db.Set<FinalInvoiceItem>()
            .Where(finalItem => finalItem.InvoiceItemId == itemId)
            .ToListAsync();
        _db.Set<FinalInvoiceItem>().RemoveRange(finalItems);

        var itemPermissions = await _db.Set<EntityPermission>()
            .Where(permission => permission.EntityName == "InvoiceItem" && permission.EntityId == itemId.ToString())
            .ToListAsync();
        _db.Set<EntityPermission>().RemoveRange(itemPermissions);

        _db.Set<InvoiceItem>().Remove(item);
        invoice.TotalAmount = invoice.InvoiceItems.Where(invoiceItem => invoiceItem.Id != itemId).Sum(invoiceItem => invoiceItem.TotalPrice);

        var finalInvoices = await _db.Set<FinalInvoice>()
            .Where(finalInvoice => finalInvoice.ProformaInvoiceId == id)
            .ToListAsync();
        foreach (var finalInvoice in finalInvoices)
            finalInvoice.TotalAmount = invoice.TotalAmount;

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return new RemoveInvoiceItemResponse
        {
            RemovedItemId = itemId,
            CancelledPurchaseOrders = linkedPos.Select(po => po.PONumber).OrderBy(number => number).ToList(),
        };
    }

    private async Task<bool> CanEditItemsAsync(Invoice invoice, long userId)
    {
        if (invoice.Quote?.UserId == userId || invoice.Quote?.RFQ?.UserId == userId) return true;

        var itemIdStrings = invoice.InvoiceItems.Select(item => item.Id.ToString()).ToList();
        return await _db.Set<EntityPermission>().AnyAsync(permission => permission.UserId == userId
            && permission.Permission == "Edit"
            && ((permission.EntityName == "Invoice" && permission.EntityId == invoice.Id.ToString())
                || (permission.EntityName == "InvoiceItem" && itemIdStrings.Contains(permission.EntityId))));
    }

    public async Task<bool> UpdateAsync(long id, UpdateInvoiceRequest request)
    {
        var invoice = await _db.Set<Invoice>().Include(i => i.InvoiceItems).FirstOrDefaultAsync(i => i.Id == id);
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
            var paymentTerm = InvoicePaymentTerms.Normalize(request.PaymentStatus, request.PaymentTermDays);
            if (paymentTerm.Term == InvoicePaymentTerms.Credit)
                await InvoicePaymentTerms.EnsureCreditAvailableAsync(_db, invoice.CustomerId, invoice.TotalAmount, invoice.Id);
            invoice.PaymentStatus = paymentTerm.Term;
            invoice.PaymentTermDays = paymentTerm.Days;
            invoice.PaymentTermStartedAt = null;
            invoice.PrepaymentPercent = paymentTerm.Term == InvoicePaymentTerms.Prepayment ? request.PrepaymentPercent : null;
        }

        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Sets the B1 invoice number by hand — used both to fill one in where the source
    /// quote had none and to correct the copy inherited from it. Blank clears it. Independent
    /// of the quote's own B1 number and of any Final Invoice created from this order.
    /// </summary>
    public async Task<bool> UpdateB1InvoiceNumberAsync(long id, string? b1InvoiceNumber)
    {
        var invoice = await _db.Set<Invoice>().FindAsync(id);
        if (invoice == null) return false;

        invoice.B1InvoiceNumber = _b1Service.Normalize(b1InvoiceNumber);
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
        if (!isAdmin && invoice.Quote.UserId != userId)
        {
            var hasPermission = await _permissionService.HasPermissionAsync(userId, "Invoice", id.ToString(), "Edit");
            if (!hasPermission) return false;
        }

        // Allowed invoice workflow statuses
        var allowedStatuses = new[]
        {
            "Draft", "Waiting For Prepayment", "Running", "Finish"
        };
        if (!allowedStatuses.Contains(status)) return false;

        // Assigned users may start work; only admins may force an invoice to Finish.
        var adminOnlyStatuses = new[] { "Finish" };
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

        if (status == "Running" && invoice.PaymentStatus == InvoicePaymentTerms.Credit)
            await InvoicePaymentTerms.EnsureCreditAvailableAsync(_db, invoice.CustomerId, invoice.TotalAmount, invoice.Id);
        invoice.Status = status;

        if (status is "Running" or "Waiting For Prepayment")
        {
            foreach (var item in invoice.InvoiceItems)
                item.Status = "Sourcing";
        }

        // Stamp PaidDate when reaching the terminal Finish state
        if (status == "Finish" && invoice.PaidDate == null)
            invoice.PaidDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        // Spin up the Procurement layer as soon as the invoice enters either purchase-editable state.
        // Idempotent — safe to call even if a Procurement already exists for this invoice.
        if (status is "Waiting For Prepayment" or "Running")
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
        var invoice = await _db.Set<Invoice>().Include(i => i.InvoiceItems).FirstOrDefaultAsync(i => i.Id == id);
        if (invoice == null || invoice.IsCancelled) return false;

        invoice.IsCancelled = true;
        invoice.CancelledAt = DateTime.UtcNow;
        invoice.Status = "Cancelled";
        foreach (var invoiceItem in invoice.InvoiceItems)
            invoiceItem.Status = "Cancelled";

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
        // A cancelled Sales Order gives back any Our Stock it was holding.
        foreach (var invoiceItem in invoice.InvoiceItems)
            await _stock.ReleaseAsync(invoiceItem.Id, 0);

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

    private async Task PopulateAssignmentsAsync(
        List<InvoiceResponse> responses,
        bool includeItems)
    {
        if (responses.Count == 0) return;

        var invoiceIds = responses.Select(response => response.Id).Distinct().ToList();
        var invoiceIdStrings = invoiceIds.Select(id => id.ToString()).ToList();
        var sources = await _db.Set<InvoiceItem>()
            .AsNoTracking()
            .Where(item => invoiceIds.Contains(item.InvoiceId) && item.QuoteItem != null)
            .Select(item => new
            {
                item.InvoiceId,
                InvoiceItemId = item.Id,
                QuoteId = item.QuoteItem!.QuoteId,
                QuoteOwnerId = item.QuoteItem.Quote.UserId,
                RfqId = item.QuoteItem.Quote.RFQId,
                RfqOwnerId = item.QuoteItem.Quote.RFQ.UserId,
            })
            .ToListAsync();

        var quoteIdStrings = sources.Select(source => source.QuoteId.ToString()).Distinct().ToList();
        var rfqIdStrings = sources.Select(source => source.RfqId.ToString()).Distinct().ToList();
        var itemIdStrings = includeItems
            ? sources.Select(source => source.InvoiceItemId.ToString()).Distinct().ToList()
            : [];

        var permissions = await _db.Set<EntityPermission>()
            .AsNoTracking()
            .Where(permission =>
                (permission.EntityName == "Invoice" && invoiceIdStrings.Contains(permission.EntityId))
                || (includeItems && permission.EntityName == "InvoiceItem" && itemIdStrings.Contains(permission.EntityId))
                || (permission.EntityName == "Quote" && quoteIdStrings.Contains(permission.EntityId))
                || (permission.EntityName == "RFQ" && rfqIdStrings.Contains(permission.EntityId)))
            .Select(permission => new { permission.EntityName, permission.EntityId, permission.UserId })
            .ToListAsync();

        var allUserIds = sources.Select(source => source.QuoteOwnerId)
            .Concat(sources.Where(source => source.RfqOwnerId.HasValue).Select(source => source.RfqOwnerId!.Value))
            .Concat(permissions.Select(permission => permission.UserId))
            .Distinct()
            .ToList();
        var userNames = await _db.Set<User>().AsNoTracking()
            .Where(user => allUserIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.Name);

        List<InvoiceAssignedUserResponse> ToUsers(IEnumerable<long> ids) => ids
            .Distinct()
            .Where(userNames.ContainsKey)
            .Select(id => new InvoiceAssignedUserResponse { Id = id, Name = userNames[id] })
            .OrderBy(user => user.Name)
            .ToList();

        foreach (var response in responses)
        {
            var invoiceSources = sources.Where(source => source.InvoiceId == response.Id).ToList();
            var directIds = permissions
                .Where(permission => permission.EntityName == "Invoice" && permission.EntityId == response.Id.ToString())
                .Select(permission => permission.UserId);
            var directUsers = ToUsers(directIds);

            var quoteIds = invoiceSources.Select(source => source.QuoteId.ToString()).ToHashSet();
            var rfqIds = invoiceSources.Select(source => source.RfqId.ToString()).ToHashSet();
            var defaultIds = invoiceSources.Select(source => source.QuoteOwnerId)
                .Concat(invoiceSources.Where(source => source.RfqOwnerId.HasValue).Select(source => source.RfqOwnerId!.Value))
                .Concat(permissions.Where(permission =>
                    (permission.EntityName == "Quote" && quoteIds.Contains(permission.EntityId))
                    || (permission.EntityName == "RFQ" && rfqIds.Contains(permission.EntityId)))
                    .Select(permission => permission.UserId));

            response.DirectAssignedUsers = directUsers;
            response.AssignedUsers = directUsers.Count > 0 ? directUsers : ToUsers(defaultIds);

            if (!includeItems) continue;
            foreach (var item in response.Items)
            {
                var itemDirectIds = permissions
                    .Where(permission => permission.EntityName == "InvoiceItem" && permission.EntityId == item.Id.ToString())
                    .Select(permission => permission.UserId);
                item.DirectAssignedUsers = ToUsers(itemDirectIds);
                item.AssignedUsers = item.DirectAssignedUsers.Count > 0
                    ? item.DirectAssignedUsers
                    : response.AssignedUsers.Select(user => new InvoiceAssignedUserResponse { Id = user.Id, Name = user.Name }).ToList();
            }
        }
    }

    private static InvoiceResponse MapToResponse(
        Invoice i,
        decimal totalPaid = 0,
        IReadOnlyDictionary<long, string>? itemDisplayStatuses = null)
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
            B1ProformaInvoiceNumber = i.B1InvoiceNumber,
            TotalAmount = i.TotalAmount,
            Status = i.Status,
            IsCancelled = i.IsCancelled,
            CancelledAt = i.CancelledAt,
            PaymentStatus = i.PaymentStatus,
            PaymentTermDays = i.PaymentTermDays,
            PaymentTermStartedAt = i.PaymentTermStartedAt,
            PaymentTermDisplay = InvoicePaymentTerms.Display(i, totalPaid),
            PaymentDueWarning = InvoicePaymentTerms.IsDueWarning(i, totalPaid),
            TotalPaid = totalPaid,
            OutstandingAmount = Math.Max(0, i.TotalAmount - totalPaid),
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
            CustomerContacts = i.Customer?.Contacts,
            RfqExType = i.InvoiceItems?
                .Select(ii => ii.QuoteItem?.RFQItem?.RFQ?.ExType)
                .FirstOrDefault(x => x.HasValue),
            DefaultDepositWalletId = i.DefaultDepositWalletId,
            DefaultBankAccountId = i.DefaultBankAccountId,
            QuoteCoefYuan = i.Quote?.CoefYuan,
            QuoteExchangeRateYuan = i.Quote?.ExchangeRateYuan,
            Items = i.InvoiceItems?.Select(ii => new InvoiceItemResponse
            {
                Id = ii.Id,
                Qty = ii.Qty,
                UnitPrice = ii.UnitPrice,
                TotalPrice = ii.TotalPrice,
                Discount = ii.Discount,
                Status = itemDisplayStatuses?.GetValueOrDefault(ii.Id) ?? ii.Status,
                OriginalUnitPrice = ii.QuoteItem?.UnitPrice,
                ExpectedDeliveryDate = ii.ExpectedDeliveryDate,
                QuoteItemId = ii.QuoteItemId,
                RFQReference = ii.QuoteItem?.RFQItemId.HasValue == true &&
                               rfqItemRank.TryGetValue(ii.QuoteItem.RFQItemId!.Value, out var rank)
                               ? rank.ToString() : null,
                PartNumberName = ii.QuoteItem?.PartNumber?.Name ?? "",
                Alt = ii.QuoteItem?.Alt,
                Description = ii.QuoteItem?.PartNumber?.Description ?? "",
                Condition = ii.Condition ?? ii.QuoteItem?.Condition,
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
