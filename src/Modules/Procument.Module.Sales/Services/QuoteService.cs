using System.Data;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Catalog.Entities;
using Procument.Module.Identity.Entities;
using Procument.Module.RFQ.Entities;
using Procument.Module.Sales.DTOs;
using Procument.Module.Sales.Entities;
using Procument.Module.Identity.Services;
using Procument.Module.Sales.Enums;
using Procument.Shared.DTOs;

namespace Procument.Module.Sales.Services;

public interface IQuoteService
{
    Task<List<QuoteResponse>> GetByRFQIdAsync(long rfqId, long userId, bool isAdmin, int[]? userBases = null);
    Task<QuoteResponse?> GetByIdAsync(long id, long userId, bool isAdmin, int[]? userBases = null);
    Task<QuoteResponse> CreateAsync(CreateQuoteRequest request, long userId);
    Task<PagedResult<QuoteResponse>> GetAllAsync(int page, int pageSize, long userId, bool isSuperAdmin, int[] userBases, List<string>? statuses = null, string? search = null, string? pnSearch = null, List<string>? assignedUserNames = null, List<string>? customerNames = null, List<string>? rfqNames = null, string? sortBy = null, bool sortDesc = false, List<string>? quoteNumbers = null, bool includeRejected = false);
    Task<bool> DeleteAsync(long id);
    Task<bool> UpdateStatusAsync(long id, string newStatus, long userId, bool isAdmin, string? rejectionNote = null);
    Task<bool> UpdateQuoteTypeAsync(long id, int? newStatus,string additional, long userId, bool isAdmin);
    Task<QuoteResponse?> UpdateAsync(long id, CreateQuoteRequest request, long userId, bool isAdmin);
    Task<bool> UpdateItemsOrderAsync(long quoteId, List<QuoteItemOrderEntry> items, long userId, bool isAdmin);
    Task<bool> UpdateRFQExTypeAsync(long quoteId, int? exType, long userId, bool isAdmin);
    Task<bool> UpdateYuanSettingsAsync(long quoteId, decimal? coefYuan, decimal? exchangeRateYuan);
}

public class QuoteService : IQuoteService
{
    private readonly DbContext _db;
    private readonly IPermissionService _permissionService;

    public QuoteService(DbContext db, IPermissionService permissionService)
    {
        _db = db;
        _permissionService = permissionService;
    }

    public async Task<List<QuoteResponse>> GetByRFQIdAsync(long rfqId, long userId, bool isAdmin, int[]? userBases = null)
    {
        // isAdmin here means Admin OR SuperAdmin — bypass for write-level access roles
        if (!isAdmin)
        {
            var rfq = await _db.Set<RFQHeader>().Include(r => r.Customer).FirstOrDefaultAsync(r => r.Id == rfqId);
            if (rfq == null) return new List<QuoteResponse>();

            bool inBase = userBases == null || rfq.Customer?.Base == null || userBases.Contains(rfq.Customer.Base.Value);
            bool isCreator = rfq.UserId == userId;
            if (!inBase && !isCreator)
            {
                var hasPermission = await _permissionService.HasPermissionAsync(userId, "RFQ", rfqId.ToString(), "View")
                                 || await _permissionService.HasPermissionAsync(userId, "RFQ", rfqId.ToString(), "Edit");
                if (!hasPermission) return new List<QuoteResponse>();
            }
        }

        var quotes = await _db.Set<Quote>()
            .Include(q => q.Customer)
            .Include(q => q.User)
            .Include(q => q.RFQ)
                .ThenInclude(r => r!.RFQItems)
            .Include(q => q.QuoteItems)
                .ThenInclude(qi => qi.PartNumber)
            .Include(q => q.QuoteItems)
                .ThenInclude(qi => qi.ProcumentRecord!)
                    .ThenInclude(pr => pr.Supplier)
            .Include(q => q.QuoteItems)
                .ThenInclude(qi => qi.RFQItem)
                    .ThenInclude(ri => ri!.RFQ)
            .Where(q => q.RFQId == rfqId)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();

        var rfqIds = quotes.Select(q => q.RFQId).Distinct();
        var rfqUserMap = await LoadRfqAssignedUsersAsync(rfqIds);
        return quotes.Select(q => MapToResponse(q, rfqUserMap.TryGetValue(q.RFQId, out var users) ? users : null)).ToList();
    }

    public async Task<QuoteResponse?> GetByIdAsync(long id, long userId, bool isAdmin, int[]? userBases = null)
    {
        var quote = await _db.Set<Quote>()
            .AsNoTrackingWithIdentityResolution()
            .Include(q => q.Customer)
            .Include(q => q.User)
            .Include(q => q.RFQ)
                .ThenInclude(r => r!.RFQItems)
            .Include(q => q.QuoteItems)
                .ThenInclude(qi => qi.PartNumber)
            .Include(q => q.QuoteItems)
                .ThenInclude(qi => qi.ProcumentRecord!)
                    .ThenInclude(pr => pr.Supplier)
            .Include(q => q.QuoteItems)
                .ThenInclude(qi => qi.RFQItem)
                    .ThenInclude(ri => ri!.RFQ)
            .FirstOrDefaultAsync(q => q.Id == id);

        if (quote == null) return null;

        if (!isAdmin)
        {
            bool inBase = userBases == null || quote.Customer?.Base == null || userBases.Contains(quote.Customer.Base.Value);
            if (!inBase && quote.UserId != userId)
            {
                var rfq = await _db.Set<RFQHeader>().FirstOrDefaultAsync(r => r.Id == quote.RFQId);
                if (rfq == null || rfq.UserId != userId)
                {
                    var hasPermission = await _permissionService.HasPermissionAsync(userId, "RFQ", quote.RFQId.ToString(), "View")
                                     || await _permissionService.HasPermissionAsync(userId, "RFQ", quote.RFQId.ToString(), "Edit");
                    if (!hasPermission) return null;
                }
            }
        }

        if (quote == null) return null;
        var rfqUserMap = await LoadRfqAssignedUsersAsync([quote.RFQId]);
        var assignedUsers = rfqUserMap.TryGetValue(quote.RFQId, out var users) ? users : null;
        return MapToResponse(quote, assignedUsers);
    }

    public async Task<QuoteResponse> CreateAsync(CreateQuoteRequest request, long userId)
    {
        // Get the RFQ to resolve customer
        var rfq = await _db.Set<RFQHeader>()
            .FirstOrDefaultAsync(r => r.Id == request.RFQId)
            ?? throw new KeyNotFoundException("RFQ not found.");

        // Prevent duplicate active quotes: block only if a non-rejected quote exists.
        // Rejected quotes are kept for history; a new quote can be created after rejection.
        var hasActiveQuote = await _db.Set<Quote>()
            .AnyAsync(q => q.RFQId == request.RFQId && q.Status != "Rejected");
        if (hasActiveQuote)
            throw new InvalidOperationException("A quote has already been created for this RFQ.");

        // Build quote items
        var quoteItems = new List<QuoteItem>();
        decimal totalAmount = 0;

        foreach (var (itemReq, index) in request.Items.Select((req, i) => (req, i)))
        {
            var rfqItem = await _db.Set<RFQItem>()
                .Include(i => i.PartNumber)
                .FirstOrDefaultAsync(i => i.Id == itemReq.RFQItemId)
                ?? throw new KeyNotFoundException($"RFQ Item {itemReq.RFQItemId} not found.");

            var totalPrice = itemReq.Qty * itemReq.UnitPrice;
            totalAmount += totalPrice;

            quoteItems.Add(new QuoteItem
            {
                RFQItemId = itemReq.RFQItemId,
                PartNumberId = rfqItem.PartNumberId,
                ProcumentRecordId = itemReq.ProcumentRecordId,
                Qty = itemReq.Qty,
                UnitPrice = itemReq.UnitPrice,
                TotalPrice = totalPrice,
                Condition = itemReq.Condition,
                Alt = itemReq.Alt,
                LeadTimeDays = itemReq.LeadTimeDays,
                SortOrder = index
            });
        }

        var quote = new Quote
        {
            QuoteNumber = string.Empty, // Will be set after SaveChanges using the auto-increment Id
            RFQId = request.RFQId,
            CustomerId = rfq.CustomerId,
            UserId = userId,
            TotalAmount = request.FinalPrice ?? totalAmount,
            FinalPrice = request.FinalPrice,
            ValidUntil = request.ValidUntil,
            Status = "Draft",
            CreatedAt = DateTime.UtcNow,
            QuoteItems = quoteItems
        };

        _db.Set<Quote>().Add(quote);
        await _db.SaveChangesAsync();

        // Set quote number based on auto-increment Id
        quote.QuoteNumber = $"QT-{quote.Id}";

        // Set RFQ status to Ready To Quote
        rfq.Status = "Ready To Quote";

        await _db.SaveChangesAsync();

        return await GetByIdAsync(quote.Id, userId, true)
            // passing isAdmin=true here to ensure we fetch it back, though userId check handles it too.
            // actually if we pass userId, it should work.
            // But 'isAdmin' param is "bypass permission check". 
            // Since we just created it, we are the owner, so userId matching works.
            // But safe to pass true to avoid overhead.
            ?? throw new Exception("Failed to load created quote.");
    }

    public async Task<PagedResult<QuoteResponse>> GetAllAsync(int page, int pageSize, long userId, bool isSuperAdmin, int[] userBases, List<string>? statuses = null, string? search = null, string? pnSearch = null, List<string>? assignedUserNames = null, List<string>? customerNames = null, List<string>? rfqNames = null, string? sortBy = null, bool sortDesc = false, List<string>? quoteNumbers = null, bool includeRejected = false)
    {
        IQueryable<Quote> query = _db.Set<Quote>()
            .AsNoTracking()
            .Include(q => q.Customer)
            .Include(q => q.User)
            .Include(q => q.RFQ)
                .ThenInclude(r => r!.RFQItems)
            .Include(q => q.QuoteItems)
                .ThenInclude(qi => qi.PartNumber)
            .Include(q => q.QuoteItems)
                .ThenInclude(qi => qi.ProcumentRecord!)
                    .ThenInclude(pr => pr.Supplier)
            .Include(q => q.QuoteItems)
                .ThenInclude(qi => qi.RFQItem)
                    .ThenInclude(ri => ri!.RFQ);

        if (statuses?.Count > 0)
            query = query.Where(q => statuses.Contains(q.Status));
        else if (!includeRejected)
            query = query.Where(q => q.Status != "Rejected");

        if (!string.IsNullOrEmpty(search))
        {
            var s = search.Trim();
            query = query.Where(q =>
                q.QuoteNumber.Contains(s) ||
                q.Status.Contains(s) ||
                (q.Customer != null && (q.Customer.Name.Contains(s) || (q.Customer.CustomerCode != null && q.Customer.CustomerCode.Contains(s)))) ||
                (q.RFQ != null && q.RFQ.Name.Contains(s)) ||
                q.QuoteItems.Any(qi =>
                    (qi.PartNumber != null && qi.PartNumber.Name.Contains(s)) ||
                    (qi.PartNumber != null && qi.PartNumber.Description != null && qi.PartNumber.Description.Contains(s)) ||
                    (qi.Alt != null && qi.Alt.Contains(s)) ||
                    (qi.Condition != null && qi.Condition.Contains(s))));
        }

        if (!string.IsNullOrEmpty(pnSearch))
            query = query.Where(q => q.QuoteItems.Any(qi => qi.PartNumber != null && qi.PartNumber.Name.Contains(pnSearch)));
        List<long> rfqIds = new List<long>();
        if (assignedUserNames?.Count > 0)
        {
            var rfqIdStrings = await _db.Set<EntityPermission>()
                .Include(p => p.User)
                .Where(p => p.EntityName == "RFQ" && assignedUserNames.Contains(p.User.Name))
                .Select(p => p.EntityId)
                .ToListAsync();
            rfqIds = rfqIdStrings.Select(id => long.TryParse(id, out var l) ? l : -1L).Where(id => id > 0).ToList();
            query = query.Where(q => rfqIds.Contains(q.RFQId));
        }

        if (customerNames?.Count > 0)
        {
            var hasNullPlaceholder = customerNames.Contains("-") || customerNames.Contains("—");
            query = query.Where(q => q.Customer != null && (
                customerNames.Contains(q.Customer.Name) || 
                (q.Customer.CustomerCode != null && customerNames.Contains(q.Customer.CustomerCode)) ||
                (hasNullPlaceholder && (q.Customer.CustomerCode == null || q.Customer.CustomerCode == ""))
            ));
        }

        if (quoteNumbers?.Count > 0)
            query = query.Where(q => quoteNumbers.Contains(q.QuoteNumber));

        if (rfqNames?.Count > 0)
        {
            var rfqIdsFromNames = rfqNames
                .Where(n => n.StartsWith("RFQ #"))
                .Select(n => n.Substring(5))
                .Select(idStr => long.TryParse(idStr, out var id) ? id : -1L)
                .Where(id => id > 0)
                .ToList();

            query = query.Where(q => 
                (q.RFQ != null && rfqNames.Contains(q.RFQ.Name)) ||
                rfqIdsFromNames.Contains(q.RFQId)
            );
        }

        if (!isSuperAdmin)
        {
            var permittedQuoteIdsStr = await _db.Set<EntityPermission>()
                .Where(p => p.UserId == userId && p.EntityName == "Quote")
                .Select(p => p.EntityId)
                .ToListAsync();

            var permittedQuoteIds = permittedQuoteIdsStr
                .Select(id => long.TryParse(id, out var l) ? l : -1)
                .ToList();

            var permittedRfqIdsStr = await _db.Set<EntityPermission>()
                .Where(p => p.UserId == userId && p.EntityName == "RFQ")
                .Select(p => p.EntityId)
                .ToListAsync();

            var permittedRfqIds = permittedRfqIdsStr
                .Select(id => long.TryParse(id, out var l) ? l : -1)
                .ToList();

            var assignedCustomerIds = await GetUserAssignedCustomerIdsAsync(userId);

            query = query.Where(q =>
                q.Customer == null ||
                q.Customer.Base == null ||
                userBases.Contains(q.Customer.Base.Value) ||
                assignedCustomerIds.Contains(q.Customer.Id) ||
                permittedQuoteIds.Contains(q.Id) ||
                permittedRfqIds.Contains(q.RFQId) ||
                q.UserId == userId);
        }

        query = sortBy switch
        {
            "quoteNumber" => sortDesc ? query.OrderByDescending(q => q.QuoteNumber) : query.OrderBy(q => q.QuoteNumber),
            "rfqName"     => sortDesc ? query.OrderByDescending(q => q.RFQ != null ? q.RFQ.Name : "") : query.OrderBy(q => q.RFQ != null ? q.RFQ.Name : ""),
            "customerCode" => sortDesc ? query.OrderByDescending(q => q.Customer != null ? q.Customer.CustomerCode : "") : query.OrderBy(q => q.Customer != null ? q.Customer.CustomerCode : ""),
            "totalAmount" => sortDesc ? query.OrderByDescending(q => q.TotalAmount) : query.OrderBy(q => q.TotalAmount),
            "status"      => sortDesc ? query.OrderByDescending(q => q.Status) : query.OrderBy(q => q.Status),
            "sentAt"      => sortDesc ? query.OrderByDescending(q => q.SentAt) : query.OrderBy(q => q.SentAt),
            "createdAt"   => sortDesc ? query.OrderByDescending(q => q.CreatedAt) : query.OrderBy(q => q.CreatedAt),
            _             => query.OrderByDescending(q => q.CreatedAt),
        };

        var totalCount = await query.CountAsync();
        var items = await query
            .ApplyPaging(page, pageSize)
            .ToListAsync();

        var rfqIdsString = items.Select(q => q.RFQId).Distinct();
        var rfqUserMap = await LoadRfqAssignedUsersAsync(rfqIdsString);

        return new PagedResult<QuoteResponse>
        {
            Items = items.Select(q => MapToResponse(q, rfqUserMap.TryGetValue(q.RFQId, out var users) ? users : null)).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var quote = await _db.Set<Quote>().FindAsync(id);
        if (quote == null) return false;

        _db.Set<Quote>().Remove(quote);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateStatusAsync(long id, string newStatus, long userId, bool isAdmin, string? rejectionNote = null)
    {
        var allowedStatuses = new[] { "Draft", "Sent", "Accepted", "Rejected" };
        if (!allowedStatuses.Contains(newStatus)) return false;

        var quote = await _db.Set<Quote>().FindAsync(id);
        if (quote == null) return false;

        // Only admin can change to Accepted or Rejected
        if ((newStatus == "Accepted" || newStatus == "Rejected") && !isAdmin) return false;

        // Non-admin can only change their own quotes
        if (!isAdmin && quote.UserId != userId) return false;

        quote.Status = newStatus;
        quote.ModifyAt = DateTime.UtcNow;

        if (newStatus == "Sent")
        {
            quote.SentAt = DateTime.UtcNow;
        }

        if (newStatus == "Rejected")
        {
            quote.RejectionNote = rejectionNote;
        }
        else
        {
            quote.RejectionNote = null;
        }

        // Cascade status to parent RFQ
        var rfq = await _db.Set<RFQHeader>().FindAsync(quote.RFQId);
        if (rfq != null)
        {
            rfq.Status = newStatus switch
            {
                "Sent" => "Sent",
                "Accepted" => "Accepted",
                "Rejected" => "Rejected",
                _ => rfq.Status // no change for Draft
            };
        }

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<QuoteResponse?> UpdateAsync(long id, CreateQuoteRequest request, long userId, bool isAdmin)
    {
        var quote = await _db.Set<Quote>()
            .Include(q => q.QuoteItems)
            .FirstOrDefaultAsync(q => q.Id == id);
        if (quote == null) return null;

        // Rejected quotes are historical records — they cannot be edited.
        // Create a new quote instead.
        if (quote.Status == "Rejected") return null;

        // Only owner or admin can edit
        if (!isAdmin && quote.UserId != userId) return null;

        // Remove old items
        _db.Set<QuoteItem>().RemoveRange(quote.QuoteItems);

        // Build new items
        decimal totalAmount = 0;
        var newItems = new List<QuoteItem>();

        foreach (var (itemReq, index) in request.Items.Select((req, i) => (req, i)))
        {
            var rfqItem = await _db.Set<RFQItem>()
                .Include(i => i.PartNumber)
                .FirstOrDefaultAsync(i => i.Id == itemReq.RFQItemId);
            if (rfqItem == null) continue;

            var totalPrice = itemReq.Qty * itemReq.UnitPrice;
            totalAmount += totalPrice;

            newItems.Add(new QuoteItem
            {
                RFQItemId = itemReq.RFQItemId,
                PartNumberId = rfqItem.PartNumberId,
                ProcumentRecordId = itemReq.ProcumentRecordId,
                Qty = itemReq.Qty,
                UnitPrice = itemReq.UnitPrice,
                TotalPrice = totalPrice,
                Condition = itemReq.Condition,
                Alt = itemReq.Alt,
                LeadTimeDays = itemReq.LeadTimeDays,
                SortOrder = index
            });
        }

        quote.TotalAmount = request.FinalPrice ?? totalAmount;
        quote.FinalPrice = request.FinalPrice;
        quote.ValidUntil = request.ValidUntil;
        quote.ModifyAt = DateTime.UtcNow;
        quote.Status = "Draft";
        quote.RejectionNote = null;
        quote.QuoteItems = newItems;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(quote.Id, userId, true);
    }

    public async Task<bool> UpdateItemsOrderAsync(long quoteId, List<QuoteItemOrderEntry> items, long userId, bool isAdmin)
    {
        var quote = await _db.Set<Quote>()
            .Include(q => q.QuoteItems)
            .FirstOrDefaultAsync(q => q.Id == quoteId);
        if (quote == null) return false;

        if (!isAdmin && quote.UserId != userId)
        {
            var hasPermission = await _permissionService.HasPermissionAsync(userId, "RFQ", quote.RFQId.ToString(), "Edit");
            if (!hasPermission) return false;
        }

        var orderMap = items.ToDictionary(i => i.Id, i => i.SortOrder);
        foreach (var qi in quote.QuoteItems)
        {
            if (orderMap.TryGetValue(qi.Id, out var so))
                qi.SortOrder = so;
        }
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Batch-loads RFQ assigned users (via EntityPermission) for a set of RFQ IDs.
    /// Returns a dictionary: rfqId → distinct list of (Id, Name).
    /// </summary>
    private async Task<Dictionary<long, List<QuoteAssignedUserResponse>>> LoadRfqAssignedUsersAsync(IEnumerable<long> rfqIds)
    {
        var rfqIdStrings = rfqIds.Select(id => id.ToString()).ToList();
        if (rfqIdStrings.Count == 0) return new();

        var permissions = await _db.Set<EntityPermission>()
            .Include(p => p.User)
            .Where(p => p.EntityName == "RFQ" && rfqIdStrings.Contains(p.EntityId))
            .ToListAsync();

        return permissions
            .GroupBy(p => long.TryParse(p.EntityId, out var id) ? id : -1L)
            .Where(g => g.Key >= 0)
            .ToDictionary(
                g => g.Key,
                g => g.Select(p => new QuoteAssignedUserResponse { Id = p.User.Id, Name = p.User.Name })
                       .GroupBy(u => u.Id)
                       .Select(ug => ug.First())
                       .ToList()
            );
    }

    private static QuoteResponse MapToResponse(Quote q, List<QuoteAssignedUserResponse>? assignedUsers = null)
    {
        // Build a 1-based rank map using the FULL ordered RFQ item list so that
        // item positions are preserved even when only a subset is quoted.
        // e.g. if RFQ has items 1,2,3,4 and the quote covers items 1,2,4 → ranks are 1,2,4 (not 1,2,3)
        var allRfqItems = q.RFQ?.RFQItems?.OrderBy(i => i.Id).ToList() ?? [];
        var rfqItemRank = allRfqItems
            .Select((item, idx) => new { item.Id, rank = idx + 1 })
            .ToDictionary(x => x.Id, x => x.rank);

        return new()
        {
            Id = q.Id,
            QuoteNumber = q.QuoteNumber,
            TotalAmount = q.TotalAmount,
            Status = q.Status,
            ValidUntil = q.ValidUntil,
            CreatedAt = q.CreatedAt,
            RFQId = q.RFQId,
            Type = q.Type,
            TypeAdditional = q.TypeAdditional,
            CustomerName = q.Customer.Name,
            CustomerCode = q.Customer.CustomerCode,
            CustomerContactPerson = q.Customer.ContactPerson,
            CustomerBillTo = q.Customer.BillTo,
            CustomerShipTo = q.Customer.ShipTo,
            CustomerTermsAndConditions = q.Customer.TermsAndConditions,
            CustomerCurrencyType = q.Customer.CurrencyType,
            CustomerBase = q.Customer.Base,
            CustomerId = q.Customer.Id,
            UserName = q.User?.Name,
            AssignedUsers = assignedUsers ?? new(),
            RejectionNote = q.RejectionNote,
            RFQName = q.RFQ?.Name,
            FinalPrice = q.FinalPrice,
            SentAt = q.SentAt,
            RFQExType = q.RFQ?.ExType,
            CoefYuan = q.CoefYuan,
            ExchangeRateYuan = q.ExchangeRateYuan,
            Items = q.QuoteItems
                //.OrderBy(qi => qi.SortOrder)
                .OrderBy(qi => qi.RFQItemId)
                .Select(qi => new QuoteItemResponse
            {
                Id = qi.Id,
                //SortOrder = qi.SortOrder,
                PartNumberName = qi.PartNumber?.Name ?? "",
                Description = qi.PartNumber?.Description,
                PartNumberId = qi.PartNumberId,
                RFQItemId = qi.RFQItemId,
                ProcumentRecordId = qi.ProcumentRecordId,
                Alt = qi.Alt,
                Qty = qi.Qty,
                UnitPrice = qi.UnitPrice,
                TotalPrice = qi.TotalPrice,
                Condition = qi.Condition,
                LeadTimeDays = qi.LeadTimeDays,
                LeadTime = qi.ProcumentRecord?.LeadTime,
                Note = qi.ProcumentRecord?.Note,
                RFQReference = qi.RFQItemId.HasValue && rfqItemRank.TryGetValue(qi.RFQItemId.Value, out var rank)
                    ? rank.ToString()
                    : null,
                TagDate = qi.ProcumentRecord?.TagDate?.ToString("yyyy-MM-dd"),
                CertName = qi.ProcumentRecord?.CertName,
                BuyPrice = qi.ProcumentRecord?.Price,
                SupplierName = qi.ProcumentRecord?.Supplier?.Name,
                ShippingCost = qi.ProcumentRecord?.ShippingCost,
                FixPrice = qi.ProcumentRecord?.FixPrice,
                ProcumentRecordSortOrder = qi.ProcumentRecord?.SortOrder ?? 0,
            }).ToList()
        };
    }

    public async Task<bool> UpdateQuoteTypeAsync(long id, int? newType,string? additional, long userId, bool isAdmin)
    {
        
        var quote = await _db.Set<Quote>().FindAsync(id);
        if (quote == null) return false;

        // Only owner or admin can change status
        if (!isAdmin && quote.UserId != userId) return false;

        quote.Type = newType;
        quote.TypeAdditional = additional;
        quote.ModifyAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateRFQExTypeAsync(long quoteId, int? exType, long userId, bool isAdmin)
    {
        var quote = await _db.Set<Quote>()
            .Include(q => q.RFQ)
            .FirstOrDefaultAsync(q => q.Id == quoteId);
        
        if (quote == null || quote.RFQ == null) return false;

        if (!isAdmin && quote.UserId != userId)
        {
            var hasPermission = await _permissionService.HasPermissionAsync(userId, "RFQ", quote.RFQId.ToString(), "Edit");
            if (!hasPermission) return false;
        }

        quote.RFQ.ExType = exType;
        quote.RFQ.ModifyAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateYuanSettingsAsync(long quoteId, decimal? coefYuan, decimal? exchangeRateYuan)
    {
        var quote = await _db.Set<Quote>().FindAsync(quoteId);
        if (quote == null) return false;
        quote.CoefYuan = coefYuan;
        quote.ExchangeRateYuan = exchangeRateYuan;
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Raw SQL cross-module helper: fetches individually assigned customer IDs for a user
    /// from the Identity module's UserCustomers table without importing Identity entities.
    /// </summary>
    private async Task<List<long>> GetUserAssignedCustomerIdsAsync(long userId)
    {
        if (userId <= 0) return [];
        var conn = _db.Database.GetDbConnection();
        var wasOpen = conn.State == System.Data.ConnectionState.Open;
        if (!wasOpen) await conn.OpenAsync();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT CustomerId FROM UserCustomers WHERE UserId = @userId";
            var param = cmd.CreateParameter();
            param.ParameterName = "@userId";
            param.Value = userId;
            cmd.Parameters.Add(param);
            using var reader = await cmd.ExecuteReaderAsync();
            var ids = new List<long>();
            while (await reader.ReadAsync())
                ids.Add(reader.GetInt64(0));
            return ids;
        }
        finally
        {
            if (!wasOpen) await conn.CloseAsync();
        }
    }
}
