using Microsoft.EntityFrameworkCore;
using Procument.Module.Catalog.Entities;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Entities;
using Procument.Module.RFQ.Entities;
using Procument.Module.Identity.Entities;
using Procument.Module.Identity.Services;
using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Services;

public interface IProcumentPageService
{
    Task<List<ProcumentPageItemResponse>> GetAllItemsAsync(long userId, bool isAdmin);
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

    public async Task<List<ProcumentPageItemResponse>> GetAllItemsAsync(long userId, bool isAdmin)
    {
        // 1. Build base query for RFQ headers with items
        IQueryable<RFQHeader> rfqQuery = _db.Set<RFQHeader>()
            .Include(r => r.Customer)
            .Include(r => r.User)
            .Include(r => r.RFQItems)
                .ThenInclude(i => i.PartNumber)
                    .ThenInclude(pn => pn.Alternatives);

        // 2. Permission filter for non-admins
        if (!isAdmin)
        {
            var permittedRfqIdsStr = await _db.Set<EntityPermission>()
                .Where(p => p.UserId == userId && p.EntityName == "RFQ")
                .Select(p => p.EntityId)
                .ToListAsync();

            var permittedRfqIds = permittedRfqIdsStr
                .Select(id => long.TryParse(id, out var l) ? l : -1)
                .ToList();

            rfqQuery = rfqQuery.Where(r => r.UserId == userId || permittedRfqIds.Contains(r.Id));
        }

        var rfqs = await rfqQuery
            .OrderByDescending(r => r.Id)
            .ToListAsync();

        // 3. Collect all RFQ IDs and load permissions + supplier quotes in batch
        var rfqIds = rfqs.Select(r => r.Id).ToList();
        var rfqIdStrings = rfqIds.Select(id => id.ToString()).ToList();

        // Batch-load permissions for assigned users
        var allPermissions = await _db.Set<EntityPermission>()
            .Include(p => p.User)
            .Where(p => p.EntityName == "RFQ" && rfqIdStrings.Contains(p.EntityId))
            .ToListAsync();

        // Batch-load all supplier quotes (ProcumentRecords) for these RFQs, excluding shop records at top level
        var allRfqItemIds = rfqs.SelectMany(r => r.RFQItems.Select(i => i.Id)).ToList();
        var allSupplierQuotes = await _db.Set<ProcumentRecord>()
            .Include(r => r.Supplier)
            .Include(r => r.ShopRecords)
                .ThenInclude(s => s.Supplier)
            .Where(r => allRfqItemIds.Contains(r.RFQItemId) && (r.Type ?? "Procument") != "Shop")
            .ToListAsync();

        // 4. Build flat response
        var result = new List<ProcumentPageItemResponse>();

        foreach (var rfq in rfqs)
        {
            // Build assigned users from permissions
            var perms = allPermissions.Where(p => p.EntityId == rfq.Id.ToString()).ToList();
            var assignedUsers = perms
                .Select(p => new ProcumentPageUserResponse { Id = p.User.Id, Name = p.User.Name })
                .GroupBy(u => u.Id)
                .Select(g => g.First())
                .ToList();

            foreach (var item in rfq.RFQItems)
            {
                var quotes = allSupplierQuotes
                    .Where(q => q.RFQItemId == item.Id)
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
                        ShopRecords = (q.ShopRecords ?? new List<ProcumentRecord>())
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
        }

        return result;
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

        // 3. Recent procurement records for same/related part numbers from RFQs created within 7 days
        var relatedRfqItemIds = await _db.Set<RFQItem>()
            .Include(i => i.RFQ)
            .Where(i => relatedPnIds.Contains(i.PartNumberId)
                     && i.RFQId != excludeRfqId
                     && i.RFQ.CreatedAt >= cutoff && (i.RFQ.Status == "Open" || i.RFQ.Status == "In Progress"))
            .Select(i => i.Id)
            .ToListAsync();

        var recentRecords = await _db.Set<ProcumentRecord>()
            .Include(r => r.Supplier)
            .Include(r => r.RFQItem)
                .ThenInclude(ri => ri.RFQ)
            .Where(r => relatedRfqItemIds.Contains(r.RFQItemId) && (r.Type ?? "Procument") != "Shop")
            .OrderByDescending(r => r.Id)
            .ToListAsync();

        // Group by supplier, take the most recent record per supplier
        var recentBySupplier = recentRecords
            .GroupBy(r => r.SupplierId)
            .Select(g => g.First())
            .Select(r => new RecentSupplierQuoteDto
            {
                SupplierId = r.SupplierId,
                SupplierName = r.Supplier.Name,
                SupplierDependency = r.Supplier.Dependency,
                Qty = r.Qty,
                Price = r.Price,
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
