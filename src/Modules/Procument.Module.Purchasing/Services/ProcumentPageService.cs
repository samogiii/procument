using Microsoft.EntityFrameworkCore;
using Procument.Module.Catalog.Entities;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Entities;
using Procument.Module.RFQ.Entities;
using Procument.Module.Identity.Entities;
using Procument.Module.Identity.Services;
using Procument.Shared.DTOs;
using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Services;

public interface IProcumentPageService
{
    Task<PagedResult<ProcumentPageItemResponse>> GetAllItemsAsync(long userId, bool isSuperAdmin, int[] userBases, PageQuery page, List<string>? statuses = null, List<string>? customerSearch = null, List<long>? userIds = null, string? pnSearch = null, bool pendingOnly = false, string? sortBy = null, bool sortDesc = false, List<string>? conditions = null, List<string>? colPartNames = null, List<string>? customerCodes = null, List<long>? rfqIds = null, List<string>? rfqNames = null, bool includeNoQuote = false);
    Task<SupplierSuggestionsResponse> GetSuggestionsAsync(long partNumberId, long excludeRfqId);
}

public class ProcumentPageService : IProcumentPageService
{
    private readonly DbContext _db;
    private readonly IPermissionService _permissionService;

    public ProcumentPageService(DbContext db, IPermissionService permissionService)
    {
        _db = db;
        _permissionService = permissionService;
    }

    public async Task<PagedResult<ProcumentPageItemResponse>> GetAllItemsAsync(long userId, bool isSuperAdmin, int[] userBases, PageQuery page, List<string>? statuses = null, List<string>? customerSearch = null, List<long>? userIds = null, string? pnSearch = null, bool pendingOnly = false, string? sortBy = null, bool sortDesc = false, List<string>? conditions = null, List<string>? colPartNames = null, List<string>? customerCodes = null, List<long>? rfqIds = null, List<string>? rfqNames = null, bool includeNoQuote = false)
    {
        // 1. Build base RFQ item query
        IQueryable<RFQItem> itemQuery = _db.Set<RFQItem>()
            .AsNoTracking()
            .Include(i => i.PartNumber)
                .ThenInclude(pn => pn.Alternatives)
            .Include(i => i.RFQ)
                .ThenInclude(r => r.Customer)
            .Include(i => i.RFQ)
                .ThenInclude(r => r.User);

        // 2. Permission filter
        if (!isSuperAdmin)
        {
            var permittedRfqIdsStr = await _db.Set<EntityPermission>()
                .Where(p => p.UserId == userId && p.EntityName == "RFQ")
                .Select(p => p.EntityId)
                .ToListAsync();

            var permittedRfqIds = permittedRfqIdsStr
                .Select(id => long.TryParse(id, out var l) ? l : -1L)
                .Where(l => l > 0)
                .ToList();

            itemQuery = itemQuery.Where(i =>
                i.RFQ.Customer.Base == null ||
                userBases.Contains(i.RFQ.Customer.Base.Value) ||
                permittedRfqIds.Contains(i.RFQId) ||
                i.RFQ.UserId == userId);
        }

        // 3. Search filter
        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var s = page.Search.Trim();
            itemQuery = itemQuery.Where(i =>
                i.PartNumber.Name.Contains(s) ||
                i.RFQ.Name.Contains(s) ||
                i.RFQ.Customer.Name.Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(pnSearch))
        {
            var pn = pnSearch.Trim();
            itemQuery = itemQuery.Where(i => i.PartNumber.Name.Contains(pn));
        }

        if (statuses?.Count > 0)
            itemQuery = itemQuery.Where(i => statuses.Contains(i.RFQ.Status ?? "Open"));
        else if (!includeNoQuote)
            itemQuery = itemQuery.Where(i => i.RFQ.Status != "No Quote");

        if (customerSearch?.Count > 0)
        {
            var customers = customerSearch.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
            if (customers.Count > 0)
            {
                var hasNullPlaceholder = customers.Contains("-") || customers.Contains("—");
                itemQuery = itemQuery.Where(i => 
                    customers.Contains(i.RFQ.Customer.Name) || 
                    (i.RFQ.Customer.CustomerCode != null && customers.Contains(i.RFQ.Customer.CustomerCode)) ||
                    (hasNullPlaceholder && (i.RFQ.Customer.CustomerCode == null || i.RFQ.Customer.CustomerCode == "")));
            }
        }

        if (userIds?.Count > 0)
        {
            var rfqIdStrs = await _db.Set<EntityPermission>()
                .Where(p => p.EntityName == "RFQ" && userIds.Contains(p.UserId))
                .Select(p => p.EntityId).ToListAsync();
            var filteredRfqIds = rfqIdStrs.Select(id => long.TryParse(id, out var l) ? l : -1L).Where(l => l > 0).ToList();
            itemQuery = itemQuery.Where(i => filteredRfqIds.Contains(i.RFQId));
        }

        if (pendingOnly)
        {
            var pendingItemIds = await _db.Set<ProcumentRecord>()
                .Where(r => r.Supplier.Status == "Pending" || r.Supplier.Status == "Rejected")
                .Select(r => r.RFQItemId).Distinct().ToListAsync();
            itemQuery = itemQuery.Where(i => pendingItemIds.Contains(i.Id));
        }

        // Column filters (exact-match multi-select)
        if (conditions?.Count > 0)
            itemQuery = itemQuery.Where(i => conditions.Contains(i.Condition ?? ""));

        if (colPartNames?.Count > 0)
            itemQuery = itemQuery.Where(i => colPartNames.Contains(i.PartNumber.Name));

        if (customerCodes?.Count > 0)
            itemQuery = itemQuery.Where(i => customerCodes.Contains(i.RFQ.Customer.CustomerCode ?? ""));

        if (rfqIds?.Count > 0)
            itemQuery = itemQuery.Where(i => rfqIds.Contains(i.RFQId));

        if (rfqNames?.Count > 0)
            itemQuery = itemQuery.Where(i => rfqNames.Contains(i.RFQ.Name));

        // 4. Sort + count + paginate
        itemQuery = sortBy switch
        {
            "rfqId"         => sortDesc ? itemQuery.OrderByDescending(i => i.RFQId).ThenByDescending(i => i.Id)  : itemQuery.OrderBy(i => i.RFQId).ThenBy(i => i.Id),
            "rfqName"       => sortDesc ? itemQuery.OrderByDescending(i => i.RFQ.Name)                           : itemQuery.OrderBy(i => i.RFQ.Name),
            "partNumberName"=> sortDesc ? itemQuery.OrderByDescending(i => i.PartNumber.Name)                    : itemQuery.OrderBy(i => i.PartNumber.Name),
            "qty"           => sortDesc ? itemQuery.OrderByDescending(i => i.Qty)                                : itemQuery.OrderBy(i => i.Qty),
            "condition"     => sortDesc ? itemQuery.OrderByDescending(i => i.Condition)                          : itemQuery.OrderBy(i => i.Condition),
            "customerName"  => sortDesc ? itemQuery.OrderByDescending(i => i.RFQ.Customer.Name)                  : itemQuery.OrderBy(i => i.RFQ.Customer.Name),
            "status"        => sortDesc ? itemQuery.OrderByDescending(i => i.RFQ.Status)                         : itemQuery.OrderBy(i => i.RFQ.Status),
            "leadTime"      => sortDesc ? itemQuery.OrderByDescending(i => i.RFQ.LeadTime)                       : itemQuery.OrderBy(i => i.RFQ.LeadTime),
            "createdAt"     => sortDesc ? itemQuery.OrderByDescending(i => i.RFQ.CreatedAt)                      : itemQuery.OrderBy(i => i.RFQ.CreatedAt),
            _               => itemQuery.OrderByDescending(i => i.RFQId).ThenBy(i => i.Id),
        };

        var total = await itemQuery.CountAsync();
        var pageItems = await (page.PageSize == -1
            ? itemQuery.ToListAsync()
            : itemQuery.Skip((page.Page - 1) * page.PageSize).Take(page.PageSize).ToListAsync());

        // 5. Batch-load supplier quotes for this page's items only.
        // A price is "expired" when the cost record itself was last touched more than 14 days ago.
        // This is independent of the part's Tag Date (can be years old) and the RFQ's CreatedAt
        // (a cost may be added today to a month-old RFQ).
        var cutoff = DateTime.UtcNow.AddDays(-14);
        var allRfqItemIds = pageItems.Select(i => i.Id).ToList();
        var allSupplierQuotes = await _db.Set<ProcumentRecord>()
            .Include(r => r.Supplier)
            .Include(r => r.ShopRecords)
                .ThenInclude(s => s.Supplier)
            .Where(r => allRfqItemIds.Contains(r.RFQItemId) && (r.Type ?? "Procument") != "Shop")
            .ToListAsync();

        // 6. Batch-load permissions for this page's unique RFQ IDs
        var loadedRfqIds = pageItems.Select(i => i.RFQId).Distinct().ToList();
        var rfqIdStrings = loadedRfqIds.Select(id => id.ToString()).ToList();
        var allPermissions = await _db.Set<EntityPermission>()
            .Include(p => p.User)
            .Where(p => p.EntityName == "RFQ" && rfqIdStrings.Contains(p.EntityId))
            .ToListAsync();

        // 7. Build flat response
        var result = new List<ProcumentPageItemResponse>();

        foreach (var item in pageItems)
        {
            var rfq = item.RFQ;
            // Build assigned users from permissions
            var perms = allPermissions.Where(p => p.EntityId == rfq.Id.ToString()).ToList();
            var assignedUsers = perms
                .Select(p => new ProcumentPageUserResponse { Id = p.User.Id, Name = p.User.Name })
                .GroupBy(u => u.Id)
                .Select(g => g.First())
                .ToList();

            var quotes = allSupplierQuotes
                    .Where(q => q.RFQItemId == item.Id)
                    .OrderBy(q => q.SortOrder)
                    .ThenBy(q => q.Id)
                    .Select(q => new SupplierQuoteResponse
                    {
                        Id = q.Id,
                        RFQItemId = q.RFQItemId,
                        SupplierId = q.SupplierId,
                        SupplierName = q.Supplier.Name,
                        SupplierStatus = q.Supplier.Status ?? "Approved",
                        SupplierDependency = q.Supplier.Dependency,
                        Qty = q.Qty,
                        Price = q.Price,
                        PriceHidden = (q.UpdatedAt ?? q.CreatedAt) < cutoff,
                        Condition = q.Condition,
                        Alt = q.Alt,
                        Unit = q.Unit,
                        CertName = q.CertName,
                        Coef_1 = q.Coef_1,
                        Coef_2 = q.Coef_2,
                        Coef_3 = q.Coef_3,
                        ShippingPoint = q.ShippingPoint,
                        ShippingCost = q.ShippingCost,
                        UnitPrice = q.UnitPrice,
                        TotalPrice = q.TotalPrice,
                        TagDate = q.TagDate,
                        LeadTime = q.LeadTime,
                        Note = q.Note,
                        MyNotes = q.MyNotes,
                        IsCertificated = q.IsCertificated,
                        Type = q.Type ?? "Procument",
                        FixPrice = q.FixPrice,
                        ParentProcumentId = q.ParentProcumentId,
                        SortOrder = q.SortOrder,
                        ShopRecords = (q.ShopRecords ?? new List<ProcumentRecord>())
                            .OrderBy(s => s.SortOrder).ThenBy(s => s.Id)
                            .Select(s => new SupplierQuoteResponse
                            {
                                Id = s.Id,
                                RFQItemId = s.RFQItemId,
                                SupplierId = s.SupplierId,
                                SupplierName = s.Supplier.Name,
                                SupplierStatus = s.Supplier.Status ?? "Approved",
                                SupplierDependency = s.Supplier.Dependency,
                                Qty = s.Qty,
                                Price = s.Price,
                                PriceHidden = (s.UpdatedAt ?? s.CreatedAt) < cutoff,
                                Condition = s.Condition,
                                Alt = s.Alt,
                                Unit = s.Unit,
                                CertName = s.CertName,
                                Coef_1 = s.Coef_1,
                                Coef_2 = s.Coef_2,
                                Coef_3 = s.Coef_3,
                                ShippingPoint = s.ShippingPoint,
                                ShippingCost = s.ShippingCost,
                                UnitPrice = s.UnitPrice,
                                TotalPrice = s.TotalPrice,
                                TagDate = s.TagDate,
                                LeadTime = s.LeadTime,
                                Note = s.Note,
                                MyNotes = s.MyNotes,
                                IsCertificated = s.IsCertificated,
                                Type = s.Type ?? "Shop",
                                FixPrice = s.FixPrice,
                                ParentProcumentId = s.ParentProcumentId,
                                SortOrder = s.SortOrder,
                            })
                            .ToList(),
                    })
                    .ToList();

                result.Add(new ProcumentPageItemResponse
                {
                    RFQItemId = item.Id,
                    RFQId = rfq.Id,
                    RFQName = rfq.Name,
                    RFQStatus = rfq.Status,
                    PartNumberName = item.PartNumber.Name,
                    PartNumberId = item.PartNumberId,
                    Description = item.PartNumber.Description,
                    Qty = item.Qty,
                    Condition = item.Condition,
                    Unit = item.Unit,
                    Priority = item.Priority,
                    Note = item.Note,
                    IsHighlighted = item.IsHighlighted,
                    CustomerName = rfq.Customer.Name,
                    CustomerCode = rfq.Customer.CustomerCode,
                    CustomerBase = rfq.Customer.Base,
                    LeadTime = rfq.LeadTime,
                    CreatedAt = rfq.CreatedAt,
                    AssignedUsers = assignedUsers,
                    SupplierQuotes = quotes,
                    Alternatives = (item.PartNumber.Alternatives ?? new List<Alternative>())
                        .Select(a => new ProcumentPageAltResponse { Id = a.Id, Name = a.Name })
                        .ToList(),
                });
        }

        return new PagedResult<ProcumentPageItemResponse>
        {
            Items = result,
            TotalCount = total,
            Page = page.Page,
            PageSize = page.PageSize
        };
    }

    public async Task<SupplierSuggestionsResponse> GetSuggestionsAsync(long partNumberId, long excludeRfqId)
    {
        var cutoff = DateTime.UtcNow.AddDays(-14);

        // 1. Get this part number's alternative IDs
        var altPartNumberIds = await _db.Set<Alternative>()
            .Where(a => a.PartNumberId == partNumberId)
            .Select(a => a.PartNumberId)
            .ToListAsync();

        // Also find part numbers that have this part number as an alternative
        var partNumberName = await _db.Set<PartNumber>()
            .Where(p => p.Id == partNumberId)
            .Select(p => p.Name)
            .FirstOrDefaultAsync();

        // Collect all related part number IDs (self + those sharing alternatives)
        var relatedPnIds = new HashSet<long> { partNumberId };

        if (!string.IsNullOrEmpty(partNumberName))
        {
            // Find part numbers that have this name as an alternative
            var pnIdsWithThisAsAlt = await _db.Set<Alternative>()
                .Where(a => a.Name == partNumberName)
                .Select(a => a.PartNumberId)
                .ToListAsync();
            foreach (var id in pnIdsWithThisAsAlt) relatedPnIds.Add(id);
        }

        // Find part numbers that share any alternative with this one
        var myAlts = await _db.Set<Alternative>()
            .Where(a => a.PartNumberId == partNumberId)
            .Select(a => a.Name)
            .ToListAsync();

        if (myAlts.Count > 0)
        {
            var pnIdsShareAlt = await _db.Set<Alternative>()
                .Where(a => myAlts.Contains(a.Name))
                .Select(a => a.PartNumberId)
                .ToListAsync();
            foreach (var id in pnIdsShareAlt) relatedPnIds.Add(id);
        }

        // 2. Known suppliers from PartNumberSupplier junction table
        var knownSuppliers = await _db.Set<PartNumberSupplier>()
            .Include(ps => ps.Supplier)
            .Where(ps => relatedPnIds.Contains(ps.PartNumberId))
            .Select(ps => new KnownSupplierDto
            {
                SupplierId = ps.SupplierId,
                SupplierName = ps.Supplier.Name
            })
            .Distinct()
            .ToListAsync();

        // Deduplicate by SupplierId
        knownSuppliers = knownSuppliers
            .GroupBy(s => s.SupplierId)
            .Select(g => g.First())
            .ToList();

        // 3. Recent procurement records for same/related part numbers.
        // "Recent" is judged by the cost record's own age (UpdatedAt/CreatedAt), NOT the RFQ's date —
        // a fresh cost can be added today to an older RFQ and is still a current, useful price.
        var relatedRfqItemIds = await _db.Set<RFQItem>()
            .Where(i => relatedPnIds.Contains(i.PartNumberId)
                     && i.RFQId != excludeRfqId
                     && (i.RFQ.Status == "Open" || i.RFQ.Status == "In Progress"))
            .Select(i => i.Id)
            .ToListAsync();

        var recentRecords = await _db.Set<ProcumentRecord>()
            .Include(r => r.Supplier)
            .Include(r => r.RFQItem)
                .ThenInclude(ri => ri.RFQ)
            .Where(r => relatedRfqItemIds.Contains(r.RFQItemId)
                     && (r.Type ?? "Procument") != "Shop"
                     && (r.UpdatedAt ?? r.CreatedAt) >= cutoff)
            .OrderByDescending(r => r.Id)
            .ToListAsync();

        // One chip per unique (supplier, condition) pair — most recent record wins
        var recentBySupplier = recentRecords
            .GroupBy(r => new { r.SupplierId, Condition = (r.Condition ?? "NE").ToUpper() })
            .Select(g => g.First())
            .Select(r => new RecentSupplierQuoteDto
            {
                SupplierId = r.SupplierId,
                SupplierName = r.Supplier.Name,
                SupplierDependency = r.Supplier.Dependency,
                Qty = r.Qty,
                // Hide the price when the cost record itself was last touched more than 14 days ago.
                // Based on the cost's own age — NOT the part's Tag Date and NOT the RFQ's CreatedAt.
                Price       = (r.UpdatedAt ?? r.CreatedAt) >= cutoff ? r.Price : 0m,
                PriceHidden = (r.UpdatedAt ?? r.CreatedAt) < cutoff,
                Condition = r.Condition,
                Alt = r.Alt,
                Unit = r.Unit,
                LeadTime = r.LeadTime,
                CertName = r.CertName,
                TagDate = r.TagDate,
                ShippingCost = r.ShippingCost,
                ShippingPoint = r.ShippingPoint,
                Note = r.Note,
                MyNotes = r.MyNotes,
                RFQId = r.RFQItem.RFQId,
                RFQName = r.RFQItem.RFQ.Name
            })
            .ToList();

        return new SupplierSuggestionsResponse
        {
            KnownSuppliers = knownSuppliers,
            RecentQuotes = recentBySupplier
        };
    }
}
