using Microsoft.EntityFrameworkCore;
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
                .ThenInclude(i => i.PartNumber);

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

        // Batch-load all supplier quotes (ProcumentRecords) for these RFQs
        var allRfqItemIds = rfqs.SelectMany(r => r.RFQItems.Select(i => i.Id)).ToList();
        var allSupplierQuotes = await _db.Set<ProcumentRecord>()
            .Include(r => r.Supplier)
            .Where(r => allRfqItemIds.Contains(r.RFQItemId))
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
                    LeadTime = rfq.LeadTime,
                    CreatedAt = rfq.CreatedAt,
                    AssignedUsers = assignedUsers,
                    SupplierQuotes = quotes,
                });
            }
        }

        return result;
    }
}
