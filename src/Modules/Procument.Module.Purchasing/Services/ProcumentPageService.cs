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
    Task<PartHistoryResponse> GetPartHistoryAsync(long partNumberId);
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
                (i.Alt != null && i.Alt.Contains(s)) ||
                (i.Condition != null && i.Condition.Contains(s)) ||
                i.RFQ.Name.Contains(s) ||
                i.RFQ.Status.Contains(s) ||
                i.RFQ.Customer.Name.Contains(s) ||
                (i.RFQ.Customer.CustomerCode != null && i.RFQ.Customer.CustomerCode.Contains(s)));
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

    /// <summary>
    /// Part numbers whose history counts as this part's history: itself, part numbers that list
    /// this one as an alternative, and part numbers sharing any of its alternatives.
    /// </summary>
    private async Task<HashSet<long>> GetRelatedPartNumberIdsAsync(long partNumberId)
    {
        var partNumberName = await _db.Set<PartNumber>()
            .Where(p => p.Id == partNumberId)
            .Select(p => p.Name)
            .FirstOrDefaultAsync();

        var relatedPnIds = new HashSet<long> { partNumberId };

        if (!string.IsNullOrEmpty(partNumberName))
        {
            // Part numbers that have this name as an alternative
            var pnIdsWithThisAsAlt = await _db.Set<Alternative>()
                .Where(a => a.Name == partNumberName)
                .Select(a => a.PartNumberId)
                .ToListAsync();
            foreach (var id in pnIdsWithThisAsAlt) relatedPnIds.Add(id);
        }

        // Part numbers that share any alternative with this one
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

        return relatedPnIds;
    }

    public async Task<SupplierSuggestionsResponse> GetSuggestionsAsync(long partNumberId, long excludeRfqId)
    {
        var cutoff = DateTime.UtcNow.AddDays(-14);

        // 1. Collect all related part number IDs (self + those sharing alternatives)
        var relatedPnIds = await GetRelatedPartNumberIdsAsync(partNumberId);

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
                     && i.RFQId != excludeRfqId)
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
                PriceHidden = (r.UpdatedAt ?? r.CreatedAt) <= cutoff,
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

    /// <summary>Max cost rows returned by the history modal. Summary counts still cover everything.</summary>
    private const int HistoryRecordLimit = 500;

    /// <summary>
    /// Full read-only history for a part: every expert who owned, was assigned to, or entered costs
    /// for an RFQ containing it, plus every supplier cost row ever recorded against it. No date cutoff.
    /// </summary>
    public async Task<PartHistoryResponse> GetPartHistoryAsync(long partNumberId)
    {
        var relatedPnIds = await GetRelatedPartNumberIdsAsync(partNumberId);

        var part = await _db.Set<PartNumber>()
            .AsNoTracking()
            .Where(p => p.Id == partNumberId)
            .Select(p => new { p.Id, p.Name, p.Description, p.CreatedAt })
            .FirstOrDefaultAsync();

        if (part == null)
            return new PartHistoryResponse { PartNumberId = partNumberId };

        var relatedNames = await _db.Set<PartNumber>()
            .AsNoTracking()
            .Where(p => relatedPnIds.Contains(p.Id) && p.Id != partNumberId)
            .Select(p => p.Name)
            .ToListAsync();

        // ── 1. Every RFQ line that has ever carried this part (or a related one) ──
        var rfqLines = await _db.Set<RFQItem>()
            .AsNoTracking()
            .Where(i => relatedPnIds.Contains(i.PartNumberId))
            .Select(i => new
            {
                i.RFQId,
                RFQName = i.RFQ.Name,
                RFQStatus = i.RFQ.Status,
                RFQCreatedAt = i.RFQ.CreatedAt,
                OwnerId = i.RFQ.UserId,
                OwnerName = i.RFQ.User != null ? i.RFQ.User.Name : null,
                OwnerRole = i.RFQ.User != null ? i.RFQ.User.Role : null
            })
            .ToListAsync();

        var rfqs = rfqLines
            .GroupBy(l => l.RFQId)
            .Select(g => g.First())
            .ToList();

        var rfqIdStrs = rfqs.Select(r => r.RFQId.ToString()).ToList();

        // ── 2. Experts explicitly assigned to those RFQs ──
        var assignments = await _db.Set<EntityPermission>()
            .AsNoTracking()
            .Where(p => p.EntityName == "RFQ" && rfqIdStrs.Contains(p.EntityId))
            .Select(p => new
            {
                p.EntityId,
                p.UserId,
                UserName = p.User.Name,
                UserRole = p.User.Role,
                p.CreatedAt
            })
            .ToListAsync();

        var assignedByRfq = assignments
            .GroupBy(a => a.EntityId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(a => a.UserName).Distinct().OrderBy(n => n).ToList());

        // ── 3. Every cost row ever recorded against those part numbers ──
        var recordQuery = _db.Set<ProcumentRecord>()
            .AsNoTracking()
            .Where(r => relatedPnIds.Contains(r.RFQItem.PartNumberId));

        var totalRecordCount = await recordQuery.CountAsync();

        var records = await recordQuery
            .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .Take(HistoryRecordLimit)
            .Select(r => new PartHistoryRecordDto
            {
                Id = r.Id,
                SupplierId = r.SupplierId,
                SupplierName = r.Supplier.Name,
                SupplierDependency = r.Supplier.Dependency,
                SupplierStatus = r.Supplier.Status,
                SupplierCreatedAt = r.Supplier.CreatedAt,
                Type = r.Type ?? "Procument",
                Condition = r.Condition,
                Alt = r.Alt,
                Qty = r.Qty,
                Unit = r.Unit,
                Price = r.Price,
                CertName = r.CertName,
                TagDate = r.TagDate,
                LeadTime = r.LeadTime,
                ShippingCost = r.ShippingCost,
                ShippingPoint = r.ShippingPoint,
                Note = r.Note,
                MyNotes = r.MyNotes,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt,
                EnteredByUserId = r.UserId,
                EnteredByName = r.User != null ? r.User.Name : null,
                RFQId = r.RFQItem.RFQId,
                RFQName = r.RFQItem.RFQ.Name,
                RFQStatus = r.RFQItem.RFQ.Status,
                RFQCreatedAt = r.RFQItem.RFQ.CreatedAt,
                PartNumberName = r.RFQItem.PartNumber.Name,
                RFQOwnerName = r.RFQItem.RFQ.User != null ? r.RFQItem.RFQ.User.Name : null
            })
            .ToListAsync();

        foreach (var rec in records)
            rec.AssignedUsers = assignedByRfq.TryGetValue(rec.RFQId.ToString(), out var names)
                ? names
                : new List<string>();

        // Per-supplier aggregates come from the full set, not just the returned page.
        var supplierStats = await recordQuery
            .GroupBy(r => r.SupplierId)
            .Select(g => new
            {
                SupplierId = g.Key,
                RecordCount = g.Count(),
                FirstQuotedAt = g.Min(r => r.CreatedAt),
                LastQuotedAt = g.Max(r => r.UpdatedAt ?? r.CreatedAt),
                MinPrice = g.Min(r => r.Price),
                MaxPrice = g.Max(r => r.Price)
            })
            .ToListAsync();

        var supplierMeta = await _db.Set<Supplier>()
            .AsNoTracking()
            .Where(s => supplierStats.Select(x => x.SupplierId).Contains(s.Id))
            .Select(s => new { s.Id, s.Name, s.Dependency, s.Status, s.CreatedAt })
            .ToListAsync();

        // Suppliers linked to the part in the junction table — including ones never quoted.
        var links = await _db.Set<PartNumberSupplier>()
            .AsNoTracking()
            .Where(ps => relatedPnIds.Contains(ps.PartNumberId))
            .Select(ps => new
            {
                ps.SupplierId,
                SupplierName = ps.Supplier.Name,
                ps.Supplier.Dependency,
                ps.Supplier.Status,
                SupplierCreatedAt = ps.Supplier.CreatedAt,
                LinkedAt = ps.CreatedAt
            })
            .ToListAsync();

        var linkBySupplier = links
            .GroupBy(l => l.SupplierId)
            .ToDictionary(g => g.Key, g => g.OrderBy(l => l.LinkedAt).First());

        // Most recent price per supplier, taken from the returned rows (already newest-first).
        var lastPriceBySupplier = records
            .GroupBy(r => r.SupplierId)
            .ToDictionary(g => g.Key, g => g.First().Price);

        var conditionsBySupplier = records
            .GroupBy(r => r.SupplierId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(r => r.Condition).Where(c => !string.IsNullOrWhiteSpace(c))
                      .Select(c => c!.Trim().ToUpperInvariant()).Distinct().OrderBy(c => c).ToList());

        var certsBySupplier = records
            .GroupBy(r => r.SupplierId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(r => r.CertName).Where(c => !string.IsNullOrWhiteSpace(c))
                      .Select(c => c!.Trim()).Distinct().OrderBy(c => c).ToList());

        var suppliers = new List<PartHistorySupplierDto>();

        foreach (var stat in supplierStats)
        {
            var meta = supplierMeta.FirstOrDefault(m => m.Id == stat.SupplierId);
            linkBySupplier.TryGetValue(stat.SupplierId, out var link);
            suppliers.Add(new PartHistorySupplierDto
            {
                SupplierId = stat.SupplierId,
                SupplierName = meta?.Name ?? link?.SupplierName ?? "—",
                Dependency = meta?.Dependency ?? link?.Dependency,
                Status = meta?.Status ?? link?.Status ?? "Approved",
                SupplierCreatedAt = meta?.CreatedAt ?? link?.SupplierCreatedAt,
                LinkedAt = link?.LinkedAt,
                IsLinked = link != null,
                RecordCount = stat.RecordCount,
                FirstQuotedAt = stat.FirstQuotedAt,
                LastQuotedAt = stat.LastQuotedAt,
                LastPrice = lastPriceBySupplier.TryGetValue(stat.SupplierId, out var lp) ? lp : null,
                MinPrice = stat.MinPrice,
                MaxPrice = stat.MaxPrice,
                Conditions = conditionsBySupplier.TryGetValue(stat.SupplierId, out var conds) ? conds : new List<string>(),
                Certs = certsBySupplier.TryGetValue(stat.SupplierId, out var certs) ? certs : new List<string>()
            });
        }

        // Linked suppliers that have never been quoted for this part still belong in the list.
        foreach (var link in linkBySupplier.Values)
        {
            if (suppliers.Any(s => s.SupplierId == link.SupplierId)) continue;
            suppliers.Add(new PartHistorySupplierDto
            {
                SupplierId = link.SupplierId,
                SupplierName = link.SupplierName,
                Dependency = link.Dependency,
                Status = link.Status,
                SupplierCreatedAt = link.SupplierCreatedAt,
                LinkedAt = link.LinkedAt,
                IsLinked = true,
                RecordCount = 0
            });
        }

        suppliers = suppliers
            .OrderByDescending(s => s.LastQuotedAt ?? s.LinkedAt ?? DateTime.MinValue)
            .ToList();

        // ── 4. Experts: RFQ owners + assignees + whoever entered the costs ──
        var experts = new Dictionary<long, PartHistoryExpertDto>();

        PartHistoryExpertDto GetExpert(long userId, string name, string? role)
        {
            if (!experts.TryGetValue(userId, out var e))
            {
                e = new PartHistoryExpertDto { UserId = userId, UserName = name, Role = role };
                experts[userId] = e;
            }
            if (string.IsNullOrWhiteSpace(e.Role)) e.Role = role;
            return e;
        }

        static void Touch(PartHistoryExpertDto e, DateTime? when)
        {
            if (when == null) return;
            if (e.FirstActivity == null || when < e.FirstActivity) e.FirstActivity = when;
            if (e.LastActivity == null || when > e.LastActivity) e.LastActivity = when;
        }

        foreach (var rfq in rfqs)
        {
            if (rfq.OwnerId is not long ownerId || string.IsNullOrWhiteSpace(rfq.OwnerName)) continue;
            var e = GetExpert(ownerId, rfq.OwnerName!, rfq.OwnerRole);
            e.OwnedRfqCount++;
            Touch(e, rfq.RFQCreatedAt);
        }

        foreach (var a in assignments)
        {
            var e = GetExpert(a.UserId, a.UserName, a.UserRole);
            e.AssignedRfqCount++;
            Touch(e, a.CreatedAt);
        }

        // Cost-entry counts come from the full set so the totals stay right when records are capped.
        var recordAuthors = await recordQuery
            .Where(r => r.UserId != null)
            .GroupBy(r => r.UserId!.Value)
            .Select(g => new
            {
                UserId = g.Key,
                Count = g.Count(),
                First = g.Min(r => r.CreatedAt),
                Last = g.Max(r => r.UpdatedAt ?? r.CreatedAt)
            })
            .ToListAsync();

        var authorIds = recordAuthors.Select(a => a.UserId).ToList();
        var authorMeta = await _db.Set<User>()
            .AsNoTracking()
            .Where(u => authorIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.Role })
            .ToListAsync();

        foreach (var a in recordAuthors)
        {
            var meta = authorMeta.FirstOrDefault(m => m.Id == a.UserId);
            if (meta == null) continue;
            var e = GetExpert(a.UserId, meta.Name, meta.Role);
            e.RecordCount += a.Count;
            Touch(e, a.First);
            Touch(e, a.Last);
        }

        return new PartHistoryResponse
        {
            PartNumberId = part.Id,
            PartNumberName = part.Name,
            Description = part.Description,
            PartCreatedAt = part.CreatedAt,
            RelatedPartNumbers = relatedNames.Distinct().OrderBy(n => n).ToList(),
            TotalRfqCount = rfqs.Count,
            TotalRecordCount = totalRecordCount,
            Truncated = totalRecordCount > records.Count,
            Experts = experts.Values
                .OrderByDescending(e => e.LastActivity ?? DateTime.MinValue)
                .ToList(),
            Suppliers = suppliers,
            Records = records
        };
    }
}
