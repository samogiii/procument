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
    Task<List<QuoteResponse>> GetByRFQIdAsync(long rfqId, long userId, bool isAdmin);
    Task<QuoteResponse?> GetByIdAsync(long id, long userId, bool isAdmin);
    Task<QuoteResponse> CreateAsync(CreateQuoteRequest request, long userId);
    Task<PagedResult<QuoteResponse>> GetAllAsync(int page, int pageSize, long userId, bool isAdmin, string? status = null);
    Task<bool> DeleteAsync(long id);
    Task<bool> UpdateStatusAsync(long id, string newStatus, long userId, bool isAdmin, string? rejectionNote = null);
    Task<bool> UpdateQuoteTypeAsync(long id, int? newStatus,string additional, long userId, bool isAdmin);
    Task<QuoteResponse?> UpdateAsync(long id, CreateQuoteRequest request, long userId, bool isAdmin);
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

    public async Task<List<QuoteResponse>> GetByRFQIdAsync(long rfqId, long userId, bool isAdmin)
    {
        if (!isAdmin)
        {
            // Check access to RFQ
            var rfq = await _db.Set<RFQHeader>().FirstOrDefaultAsync(r => r.Id == rfqId);
            if (rfq == null) return new List<QuoteResponse>();

            if (rfq.UserId != userId)
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

    public async Task<QuoteResponse?> GetByIdAsync(long id, long userId, bool isAdmin)
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
            // Allow if:
            // 1. I created the quote
            // 2. I created the RFQ
            // 3. I have permission on the RFQ

            if (quote.UserId != userId)
            {
                var rfq = await _db.Set<RFQHeader>().FirstOrDefaultAsync(r => r.Id == quote.RFQId);

                // If RFQ is null (shouldn't happen), assume no access unless I created quote (checked above)
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

        foreach (var itemReq in request.Items)
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
                LeadTimeDays = itemReq.LeadTimeDays
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

    public async Task<PagedResult<QuoteResponse>> GetAllAsync(int page, int pageSize, long userId, bool isAdmin, string? status = null)
    {
        IQueryable<Quote> query = _db.Set<Quote>()
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

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(q => q.Status == status);
        }

        if (!isAdmin)
        {
            // Filter: Owner OR Assigned Permission

            // 1. Get Permitted Quote IDs
            var permittedQuoteIdsStr = await _db.Set<EntityPermission>()
                .Where(p => p.UserId == userId && p.EntityName == "Quote")
                .Select(p => p.EntityId)
                .ToListAsync();

            var permittedQuoteIds = permittedQuoteIdsStr
                .Select(id => long.TryParse(id, out var l) ? l : -1)
                .ToList();

            // 2. Also allow if user owns the Quote
            // Note: We are no longer checking RFQ permissions here based on user request/edit.
            // If strict adherence to "Creator can check it" is required:

            query = query.Where(q => q.UserId == userId || permittedQuoteIds.Contains(q.Id));
        }

        query = query.OrderByDescending(q => q.CreatedAt);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var rfqIds = items.Select(q => q.RFQId).Distinct();
        var rfqUserMap = await LoadRfqAssignedUsersAsync(rfqIds);

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

        foreach (var itemReq in request.Items)
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
                LeadTimeDays = itemReq.LeadTimeDays
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
            UserName = q.User?.Name,
            AssignedUsers = assignedUsers ?? new(),
            RejectionNote = q.RejectionNote,
            RFQName = q.RFQ?.Name,
            FinalPrice = q.FinalPrice,
            SentAt = q.SentAt,
            Items = q.QuoteItems.OrderBy(qi => qi.RFQItemId).Select(qi => new QuoteItemResponse
            {
                Id = qi.Id,
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
                FixPrice = qi.ProcumentRecord?.FixPrice
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
}
