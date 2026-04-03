using Microsoft.EntityFrameworkCore;
using Procument.Module.Catalog.Entities;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Entities;
using Procument.Module.RFQ.Entities;

using Procument.Module.Identity.Services;
using Procument.Module.Identity.Entities; // for permissions

namespace Procument.Module.Purchasing.Services;

public interface ISupplierQuoteService
{
    Task<List<SupplierQuoteResponse>> GetByRFQIdAsync(long rfqId, long userId, bool isAdmin);
    Task<SupplierQuoteResponse> SaveAsync(SaveSupplierQuoteRequest request, long userId);
    Task<List<SupplierQuoteResponse>> BulkSaveAsync(long rfqId, BulkSaveQuotesRequest request, long userId);
    Task<bool> DeleteAsync(long id, long userId);
}

public class SupplierQuoteService : ISupplierQuoteService
{
    private readonly DbContext _db;
    private readonly IPermissionService _permissionService;

    public SupplierQuoteService(DbContext db, IPermissionService permissionService)
    {
        _db = db;
        _permissionService = permissionService;
    }

    /// <summary>Get all supplier quotes for all items in an RFQ.</summary>
    public async Task<List<SupplierQuoteResponse>> GetByRFQIdAsync(long rfqId, long userId, bool isAdmin)
    {
        // 1. Check if user has access to this RFQ
        if (!isAdmin)
        {
            // We need to check if user is Creator OR has Permission.
            // We can fetch RFQ header lightly or check permission table.
            // Ideally re-use RFQService logic, but avoiding circular dep.
            // Let's fetch RFQ UserId.
            var rfq = await _db.Set<RFQHeader>().FirstOrDefaultAsync(r => r.Id == rfqId);
            if (rfq == null) return new List<SupplierQuoteResponse>(); // RFQ doesn't exist

            if (rfq.UserId != userId)
            {
                var hasPermission = await _permissionService.HasPermissionAsync(userId, "RFQ", rfqId.ToString(), "View")
                                 || await _permissionService.HasPermissionAsync(userId, "RFQ", rfqId.ToString(), "Edit");

                if (!hasPermission)
                {
                    // User has no access to this RFQ -> return empty list or throw.
                    // Empty list is safer for list endpoints.
                    return new List<SupplierQuoteResponse>();
                }
            }
        }

        var records = await _db.Set<ProcumentRecord>()
            .Include(r => r.Supplier)
            .Where(r => r.RFQItem.RFQId == rfqId)
            .ToListAsync();

        return records.Select(MapToResponse).ToList();
    }

    /// <summary>Create or update a single supplier quote.</summary>
    public async Task<SupplierQuoteResponse> SaveAsync(SaveSupplierQuoteRequest request, long userId)
    {
        // 1. Check Permissions
        // Since we don't have RFQId easily in Update case, we need to fetch it.
        long rfqId;
        if (request.Id.HasValue && request.Id > 0)
        {
            var existing = await _db.Set<ProcumentRecord>()
                .Include(r => r.RFQItem)
                .FirstOrDefaultAsync(r => r.Id == request.Id.Value);
            if (existing == null) throw new KeyNotFoundException($"Supplier quote {request.Id} not found.");
            rfqId = existing.RFQItem.RFQId;
        }
        else
        {
            // For create, we need RFQItemId to find RFQId
            var rfqItem = await _db.Set<RFQItem>().FindAsync(request.RFQItemId)
                ?? throw new KeyNotFoundException("RFQ Item not found");
            rfqId = rfqItem.RFQId;
        }

        // Check if user is Admin OR has Edit permission
        var hasPermission = await _permissionService.HasPermissionAsync(userId, "RFQ", rfqId.ToString(), "Edit");

        var user = await _db.Set<User>().FindAsync(userId);
        bool isAdmin = user?.Role == "Admin";

        // Check if user is the owner of THIS RFQ
        var isOwner = await _db.Set<RFQHeader>().AnyAsync(r => r.Id == rfqId && r.UserId == userId);

        if (!isAdmin && !hasPermission && !isOwner)
        {
            throw new UnauthorizedAccessException("User does not have permission to add quotes to this RFQ.");
        }

        // Resolve or create supplier by name
        var supplier = await ResolveSupplierAsync(request.SupplierName);

        ProcumentRecord record;

        if (request.Id.HasValue && request.Id > 0)
        {
            // Update existing
            var existingRecord = await _db.Set<ProcumentRecord>()
                .FirstOrDefaultAsync(r => r.Id == request.Id.Value);

            if (existingRecord == null) throw new KeyNotFoundException($"Procurement record {request.Id.Value} not found.");
            record = existingRecord;

            record.SupplierId = supplier.Id;
            record.Qty = request.Qty;
            record.Price = request.Price;
            record.Condition = request.Condition;
            record.Alt = request.Alt;
            record.Unit = request.Unit;
            record.LeadTime = request.LeadTime;
            record.Coef_1 = request.Coef_1;
            record.Coef_2 = request.Coef_2;
            record.Coef_3 = request.Coef_3;
            record.ShippingCost = request.ShippingCost;
            record.ShippingPoint = request.ShippingPoint;
            record.CertName = request.CertName;
            record.UnitPrice = request.UnitPrice;
            record.TotalPrice = request.TotalPrice;
            record.TagDate = request.TagDate;
            record.Note = request.Note;
            record.MyNotes = request.MyNotes;
            record.UserId = userId;
        }
        else
        {
            // Create new
            record = new ProcumentRecord
            {
                RFQItemId = request.RFQItemId,
                SupplierId = supplier.Id,
                Qty = request.Qty,
                Price = request.Price,
                Condition = request.Condition,
                Alt = request.Alt,
                Unit = request.Unit,
                LeadTime = request.LeadTime,
                Coef_1 = request.Coef_1,
                Coef_2 = request.Coef_2,
                Coef_3 = request.Coef_3,
                ShippingCost = request.ShippingCost,
                ShippingPoint = request.ShippingPoint,
                CertName = request.CertName,
                UnitPrice = request.UnitPrice,
                TotalPrice = request.TotalPrice,
                TagDate = request.TagDate,
                Note = request.Note,
                MyNotes = request.MyNotes,
                UserId = userId
            };
            _db.Set<ProcumentRecord>().Add(record);

            // We can't log ID yet.
        }

        await _db.SaveChangesAsync(); // Get ID

        if (!request.Id.HasValue)
        {
            // audit handled by controller middleware
        }

        // ── Auto-link supplier to part number in junction table ──
        {
            var rfqItemForLink = await _db.Set<RFQItem>()
                .FirstOrDefaultAsync(i => i.Id == record.RFQItemId);

            if (rfqItemForLink != null)
            {
                var alreadyLinked = await _db.Set<PartNumberSupplier>()
                    .AnyAsync(ps => ps.PartNumberId == rfqItemForLink.PartNumberId && ps.SupplierId == supplier.Id);

                if (!alreadyLinked)
                {
                    _db.Set<PartNumberSupplier>().Add(new PartNumberSupplier
                    {
                        PartNumberId = rfqItemForLink.PartNumberId,
                        SupplierId = supplier.Id,
                        CreatedAt = DateTime.UtcNow
                    });
                    await _db.SaveChangesAsync();
                }
            }
        }

        // ── Auto-add Alt P/N to Alternatives table if not exists ──
        if (!string.IsNullOrWhiteSpace(request.Alt))
        {
            var rfqItem = await _db.Set<RFQItem>()
                .FirstOrDefaultAsync(i => i.Id == request.RFQItemId);

            if (rfqItem != null)
            {
                var altTrimmed = request.Alt.Trim();
                var exists = await _db.Set<Alternative>()
                    .AnyAsync(a => a.PartNumberId == rfqItem.PartNumberId && a.Name == altTrimmed);

                if (!exists)
                {
                    _db.Set<Alternative>().Add(new Alternative
                    {
                        Name = altTrimmed,
                        PartNumberId = rfqItem.PartNumberId,
                        CreatedAt = DateTime.UtcNow
                    });
                    await _db.SaveChangesAsync();
                }
            }
        }

        // Reload with supplier navigation
        record = await _db.Set<ProcumentRecord>()
            .Include(r => r.Supplier)
            .FirstAsync(r => r.Id == record.Id);

        return MapToResponse(record);
    }

    /// <summary>Bulk save supplier quotes for an RFQ.</summary>
    public async Task<List<SupplierQuoteResponse>> BulkSaveAsync(long rfqId, BulkSaveQuotesRequest request, long userId)
    {
        // Permission check (once for the whole bulk op)
        // Permission check (once for the whole bulk op)
        var hasPermission = await _permissionService.HasPermissionAsync(userId, "RFQ", rfqId.ToString(), "Edit");
        var user = await _db.Set<User>().FindAsync(userId);
        bool isAdmin = user?.Role == "Admin";

        var isOwner = await _db.Set<RFQHeader>().AnyAsync(s => s.Id == rfqId && s.UserId == userId);

        if (!isAdmin && !hasPermission && !isOwner)
        {
            throw new UnauthorizedAccessException("User does not have permission to add quotes to this RFQ.");
        }

        var results = new List<SupplierQuoteResponse>();

        // We can reuse SaveAsync but pass userId. 
        // OPTIMIZATION: SaveAsync does permission check again. 
        // It's fine for now, or we can refactor internal method.
        // But SaveAsync needs to fetch RFQId for each item to check permission if we don't pass it.
        // Here we know RFQId.

        foreach (var quote in request.Quotes)
        {
            // We can't easily skip check inside SaveAsync without refactoring.
            // Let's just call it. Use cache if performance issue.
            var result = await SaveAsync(quote, userId);
            results.Add(result);
        }

        return results;
    }

    /// <summary>Delete a supplier quote by ID.</summary>
    public async Task<bool> DeleteAsync(long id, long userId)
    {
        var record = await _db.Set<ProcumentRecord>()
            .Include(r => r.RFQItem)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (record == null) return false;

        // Check permission
        long rfqId = record.RFQItem.RFQId;
        var hasPermission = await _permissionService.HasPermissionAsync(userId, "RFQ", rfqId.ToString(), "Edit");
        var user = await _db.Set<User>().FindAsync(userId);
        bool isAdmin = user?.Role == "Admin";

        if (!isAdmin && !hasPermission)
        {
            throw new UnauthorizedAccessException("User does not have permission to delete quotes from this RFQ.");
        }

        _db.Set<ProcumentRecord>().Remove(record);
        await _db.SaveChangesAsync();

        return true;
    }

    // ──── Helpers ────

    private async Task<Supplier> ResolveSupplierAsync(string supplierName)
    {
        var trimmed = supplierName.Trim();
        var supplier = await _db.Set<Supplier>()
            .FirstOrDefaultAsync(s => s.Name == trimmed);

        if (supplier == null)
        {
            supplier = new Supplier
            {
                Name = trimmed,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            _db.Set<Supplier>().Add(supplier);
            await _db.SaveChangesAsync();
        }

        return supplier;
    }

    private static SupplierQuoteResponse MapToResponse(ProcumentRecord r) => new()
    {
        Id = r.Id,
        RFQItemId = r.RFQItemId,
        SupplierId = r.SupplierId,
        SupplierName = r.Supplier.Name,
        Qty = r.Qty,
        Price = r.Price,
        Condition = r.Condition,
        Alt = r.Alt,
        Unit = r.Unit,
        CertName = r.CertName,
        Coef_1  = r.Coef_1,
        Coef_2 = r.Coef_2,
        Coef_3 = r.Coef_3,
        ShippingPoint = r.ShippingPoint,
        ShippingCost = r.ShippingCost,
        UnitPrice = r.UnitPrice,
        TotalPrice = r.TotalPrice,
        TagDate = r.TagDate,
        LeadTime = r.LeadTime,
        Note = r.Note,
        MyNotes = r.MyNotes,
    };
}
