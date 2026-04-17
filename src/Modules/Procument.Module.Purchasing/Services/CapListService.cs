using Microsoft.EntityFrameworkCore;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Entities;

namespace Procument.Module.Purchasing.Services;

public interface ICapListService
{
    Task<List<CapListItemResponse>> GetAllAsync();
    Task<CapListItemResponse> SaveAsync(SaveCapListItemRequest request);
    Task<bool> DeleteAsync(long id);
    Task<List<ARShopForCapListResponse>> GetARShopSuggestionsAsync();
    Task<BulkImportResult> BulkImportAsync(BulkImportCapListRequest request);
}

public class CapListService : ICapListService
{
    private readonly DbContext _db;

    public CapListService(DbContext db)
    {
        _db = db;
    }

    public async Task<List<CapListItemResponse>> GetAllAsync()
    {
        var items = await _db.Set<CapListItem>()
            .Include(i => i.PartNumber)
            .Include(i => i.Company)
            .Include(i => i.ProcumentRecord)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        return items.Select(MapToResponse).ToList();
    }

    public async Task<CapListItemResponse> SaveAsync(SaveCapListItemRequest request)
    {
        // ── Resolve or create PartNumber ──
        long partNumberId;
        if (request.PartNumberId.HasValue && request.PartNumberId.Value > 0)
        {
            partNumberId = request.PartNumberId.Value;
        }
        else if (!string.IsNullOrWhiteSpace(request.PartNumberName))
        {
            var trimmed = request.PartNumberName.Trim();
            var existing = await _db.Set<Procument.Module.Catalog.Entities.PartNumber>()
                .FirstOrDefaultAsync(p => p.Name == trimmed);
            if (existing != null)
            {
                partNumberId = existing.Id;
            }
            else
            {
                var newPn = new Procument.Module.Catalog.Entities.PartNumber
                {
                    Name = trimmed,
                    Description = request.Description,
                    CreatedAt = DateTime.UtcNow
                };
                _db.Set<Procument.Module.Catalog.Entities.PartNumber>().Add(newPn);
                await _db.SaveChangesAsync();
                partNumberId = newPn.Id;
            }
        }
        else
        {
            throw new ArgumentException("PartNumberId or PartNumberName is required");
        }

        // ── Resolve or create Company (Supplier) ──
        long companyId;
        if (request.CompanyId.HasValue && request.CompanyId.Value > 0)
        {
            companyId = request.CompanyId.Value;
        }
        else if (!string.IsNullOrWhiteSpace(request.CompanyName))
        {
            var trimmed = request.CompanyName.Trim();
            var existing = await _db.Set<Procument.Module.Catalog.Entities.Supplier>()
                .FirstOrDefaultAsync(s => s.Name == trimmed);
            if (existing != null)
            {
                companyId = existing.Id;
            }
            else
            {
                var newSupplier = new Procument.Module.Catalog.Entities.Supplier
                {
                    Name = trimmed,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };
                _db.Set<Procument.Module.Catalog.Entities.Supplier>().Add(newSupplier);
                await _db.SaveChangesAsync();
                companyId = newSupplier.Id;
            }
        }
        else
        {
            throw new ArgumentException("CompanyId or CompanyName is required");
        }

        CapListItem item;

        if (request.Id.HasValue && request.Id.Value > 0)
        {
            item = await _db.Set<CapListItem>().FindAsync(request.Id.Value)
                ?? throw new InvalidOperationException($"CapListItem {request.Id} not found");

            item.PartNumberId = partNumberId;
            item.Description = request.Description;
            item.CompanyId = companyId;
            item.IsRepair = request.IsRepair;
            item.ProcumentRecordId = request.ProcumentRecordId;
        }
        else
        {
            item = new CapListItem
            {
                PartNumberId = partNumberId,
                Description = request.Description,
                CompanyId = companyId,
                IsRepair = request.IsRepair,
                ProcumentRecordId = request.ProcumentRecordId,
                CreatedAt = DateTime.UtcNow
            };
            _db.Set<CapListItem>().Add(item);
        }

        await _db.SaveChangesAsync();

        var saved = await _db.Set<CapListItem>()
            .Include(i => i.PartNumber)
            .Include(i => i.Company)
            .FirstAsync(i => i.Id == item.Id);

        return MapToResponse(saved);
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var item = await _db.Set<CapListItem>().FindAsync(id);
        if (item == null) return false;

        _db.Set<CapListItem>().Remove(item);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<ARShopForCapListResponse>> GetARShopSuggestionsAsync()
    {
        var shops = await _db.Set<ProcumentRecord>()
            .Where(r => r.Type == "Shop")
            .Include(r => r.Supplier)
            .Include(r => r.RFQItem)
                .ThenInclude(i => i.PartNumber)
            .Include(r => r.RFQItem)
                .ThenInclude(i => i.RFQ)
            .ToListAsync();

        // Exclude shops already imported
        var importedIds = await _db.Set<CapListItem>()
            .Where(c => c.ProcumentRecordId.HasValue)
            .Select(c => c.ProcumentRecordId!.Value)
            .ToListAsync();

        return shops
            .Where(s => !importedIds.Contains(s.Id))
            .Select(s => new ARShopForCapListResponse
            {
                ProcumentRecordId = s.Id,
                PartNumberId = s.RFQItem?.PartNumberId ?? 0,
                PartNumberName = s.RFQItem?.PartNumber?.Name ?? "",
                AltPartNumber = s.Alt,
                SupplierId = s.SupplierId,
                SupplierName = s.Supplier?.Name ?? "",
                Price = s.Price,
                FixPrice = s.FixPrice,
                Qty = s.Qty,
                Condition = s.Condition,
                RFQName = s.RFQItem?.RFQ?.Name
            })
            .Where(s => s.PartNumberId > 0 && s.SupplierId > 0)
            .ToList();
    }

    public async Task<BulkImportResult> BulkImportAsync(BulkImportCapListRequest request)
    {
        var result = new BulkImportResult();
        if (request.Rows == null || !request.Rows.Any()) return result;

        // 1. Performance Tuning: Disable change tracking overhead for bulk operation
        _db.ChangeTracker.AutoDetectChangesEnabled = false;

        try
        {
            // 2. Identify all unique names in the request to minimize DB hits
            var requestedPnNames = request.Rows
                .Select(r => r.PartNumberName?.Trim())
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var requestedCompNames = request.Rows
                .Select(r => r.CompanyName?.Trim())
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // 3. Targeted Fetching: Only get what we need from DB
            var existingPns = await _db.Set<Procument.Module.Catalog.Entities.PartNumber>()
                .Where(p => requestedPnNames.Contains(p.Name))
                .ToDictionaryAsync(p => p.Name.ToLower(), p => p);

            var existingSuppliers = await _db.Set<Procument.Module.Catalog.Entities.Supplier>()
                .Where(s => requestedCompNames.Contains(s.Name))
                .ToDictionaryAsync(s => s.Name.ToLower(), s => s);

            // Temporary caches for items created during this specific import
            var newPns = new Dictionary<string, Procument.Module.Catalog.Entities.PartNumber>();
            var newSuppliers = new Dictionary<string, Procument.Module.Catalog.Entities.Supplier>();

            int batchSize = 1000;
            int counter = 0;

            foreach (var row in request.Rows)
            {
                var pnName = row.PartNumberName?.Trim();
                var compName = row.CompanyName?.Trim();

                if (string.IsNullOrWhiteSpace(pnName) || string.IsNullOrWhiteSpace(compName))
                {
                    result.Skipped++;
                    continue;
                }

                var pnLower = pnName.ToLower();
                var compLower = compName.ToLower();

                // ── Resolve or Create PartNumber ──
                if (!existingPns.TryGetValue(pnLower, out var pn) && !newPns.TryGetValue(pnLower, out pn))
                {
                    pn = new Procument.Module.Catalog.Entities.PartNumber
                    {
                        Name = pnName,
                        Description = row.Description,
                        CreatedAt = DateTime.UtcNow
                    };
                    newPns[pnLower] = pn;
                    _db.Set<Procument.Module.Catalog.Entities.PartNumber>().Add(pn);
                }

                // ── Resolve or Create Supplier ──
                if (!existingSuppliers.TryGetValue(compLower, out var supplier) && !newSuppliers.TryGetValue(compLower, out supplier))
                {
                    supplier = new Procument.Module.Catalog.Entities.Supplier
                    {
                        Name = compName,
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true,
                        Status = "Approved"
                    };
                    newSuppliers[compLower] = supplier;
                    _db.Set<Procument.Module.Catalog.Entities.Supplier>().Add(supplier);
                }

                // ── Add CapListItem ──
                var item = new CapListItem
                {
                    PartNumber = pn, // EF handles the relationship linking
                    Company = supplier,
                    Description = row.Description,
                    IsRepair = row.IsRepair,
                    CreatedAt = DateTime.UtcNow
                };
                _db.Set<CapListItem>().Add(item);
                
                result.Created++;
                counter++;

                // 4. Batch Save to prevent memory overflow
                if (counter % batchSize == 0)
                {
                    await _db.SaveChangesAsync();
                    // Optional: If memory is still high, consider disposing and recreating context
                    // or manually detaching entities. For now, batching SaveChanges is the biggest win.
                }
            }

            // Final save for remaining items
            await _db.SaveChangesAsync();
        }
        finally
        {
            _db.ChangeTracker.AutoDetectChangesEnabled = true;
        }

        return result;
    }

    private static CapListItemResponse MapToResponse(CapListItem item) => new()
    {
        Id = item.Id,
        PartNumberId = item.PartNumberId,
        PartNumberName = item.PartNumber?.Name ?? "",
        Description = item.Description,
        CompanyId = item.CompanyId,
        CompanyName = item.Company?.Name ?? "",
        IsRepair = item.IsRepair,
        Condition = item.ProcumentRecord?.Condition,
        ProcumentRecordId = item.ProcumentRecordId,
        CreatedAt = item.CreatedAt
    };
}
