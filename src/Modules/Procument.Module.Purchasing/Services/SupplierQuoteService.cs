using Microsoft.EntityFrameworkCore;
using Procument.Module.Catalog.Entities;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Entities;
using Procument.Module.RFQ.Entities;

namespace Procument.Module.Purchasing.Services;

public interface ISupplierQuoteService
{
    Task<List<SupplierQuoteResponse>> GetByRFQIdAsync(long rfqId);
    Task<SupplierQuoteResponse> SaveAsync(SaveSupplierQuoteRequest request);
    Task<List<SupplierQuoteResponse>> BulkSaveAsync(long rfqId, BulkSaveQuotesRequest request);
    Task<bool> DeleteAsync(long id);
}

public class SupplierQuoteService : ISupplierQuoteService
{
    private readonly DbContext _db;

    public SupplierQuoteService(DbContext db)
    {
        _db = db;
    }

    /// <summary>Get all supplier quotes for all items in an RFQ.</summary>
    public async Task<List<SupplierQuoteResponse>> GetByRFQIdAsync(long rfqId)
    {
        var records = await _db.Set<ProcumentRecord>()
            .Include(r => r.Supplier)
            .Where(r => r.RFQItem.RFQId == rfqId)
            .ToListAsync();

        return records.Select(MapToResponse).ToList();
    }

    /// <summary>Create or update a single supplier quote.</summary>
    public async Task<SupplierQuoteResponse> SaveAsync(SaveSupplierQuoteRequest request)
    {
        // Resolve or create supplier by name
        var supplier = await ResolveSupplierAsync(request.SupplierName);

        ProcumentRecord record;

        if (request.Id.HasValue && request.Id > 0)
        {
            // Update existing
            record = await _db.Set<ProcumentRecord>()
                .FirstOrDefaultAsync(r => r.Id == request.Id.Value)
                ?? throw new KeyNotFoundException($"Supplier quote {request.Id} not found.");

            record.SupplierId = supplier.Id;
            record.Qty = request.Qty;
            record.Price = request.Price;
            record.Condition = request.Condition;
            record.Alt = request.Alt;
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
                Alt = request.Alt
            };
            _db.Set<ProcumentRecord>().Add(record);
        }

        await _db.SaveChangesAsync();

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
    public async Task<List<SupplierQuoteResponse>> BulkSaveAsync(long rfqId, BulkSaveQuotesRequest request)
    {
        var results = new List<SupplierQuoteResponse>();

        foreach (var quote in request.Quotes)
        {
            var result = await SaveAsync(quote);
            results.Add(result);
        }

        return results;
    }

    /// <summary>Delete a supplier quote by ID.</summary>
    public async Task<bool> DeleteAsync(long id)
    {
        var record = await _db.Set<ProcumentRecord>().FindAsync(id);
        if (record == null) return false;

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
        Alt = r.Alt
    };
}
