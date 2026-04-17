using Microsoft.EntityFrameworkCore;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Entities;

namespace Procument.Module.Purchasing.Services;

public interface IInventoryService
{
    Task<List<InventoryItemResponse>> GetAllAsync();
    Task<InventoryItemResponse> SaveAsync(SaveInventoryItemRequest request);
    Task<bool> DeleteAsync(long id);
    Task<BulkImportResult> BulkImportAsync(BulkImportInventoryRequest request);
}

public class InventoryService : IInventoryService
{
    private readonly DbContext _db;

    public InventoryService(DbContext db)
    {
        _db = db;
    }

    public async Task<List<InventoryItemResponse>> GetAllAsync()
    {
        var items = await _db.Set<InventoryItem>()
            .Include(i => i.PartNumber)
            .Include(i => i.Company)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        return items.Select(MapToResponse).ToList();
    }

    public async Task<InventoryItemResponse> SaveAsync(SaveInventoryItemRequest request)
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

        InventoryItem item;

        if (request.Id.HasValue && request.Id.Value > 0)
        {
            item = await _db.Set<InventoryItem>().FindAsync(request.Id.Value)
                ?? throw new InvalidOperationException($"InventoryItem {request.Id} not found");

            item.PartNumberId = partNumberId;
            item.Description = request.Description;
            item.Qty = request.Qty;
            item.CompanyId = companyId;
            item.Condition = request.Condition;
            item.Price = request.Price;
            item.SerialNumber = request.SerialNumber;
        }
        else
        {
            item = new InventoryItem
            {
                PartNumberId = partNumberId,
                Description = request.Description,
                Qty = request.Qty,
                CompanyId = companyId,
                Condition = request.Condition,
                Price = request.Price,
                SerialNumber = request.SerialNumber,
                CreatedAt = DateTime.UtcNow
            };
            _db.Set<InventoryItem>().Add(item);
        }

        await _db.SaveChangesAsync();

        var saved = await _db.Set<InventoryItem>()
            .Include(i => i.PartNumber)
            .Include(i => i.Company)
            .FirstAsync(i => i.Id == item.Id);

        return MapToResponse(saved);
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var item = await _db.Set<InventoryItem>().FindAsync(id);
        if (item == null) return false;

        _db.Set<InventoryItem>().Remove(item);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<BulkImportResult> BulkImportAsync(BulkImportInventoryRequest request)
    {
        var result = new BulkImportResult();
        if (request.Rows == null || !request.Rows.Any()) return result;

        _db.ChangeTracker.AutoDetectChangesEnabled = false;

        try
        {
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

            var existingPns = await _db.Set<Procument.Module.Catalog.Entities.PartNumber>()
                .Where(p => requestedPnNames.Contains(p.Name))
                .ToDictionaryAsync(p => p.Name.ToLower(), p => p);

            var existingSuppliers = await _db.Set<Procument.Module.Catalog.Entities.Supplier>()
                .Where(s => requestedCompNames.Contains(s.Name))
                .ToDictionaryAsync(s => s.Name.ToLower(), s => s);

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

                _db.Set<InventoryItem>().Add(new InventoryItem
                {
                    PartNumber = pn,
                    Company = supplier,
                    Description = row.Description,
                    Qty = row.Qty,
                    Condition = row.Condition,
                    Price = row.Price,
                    SerialNumber = row.SerialNumber,
                    CreatedAt = DateTime.UtcNow
                });

                result.Created++;
                counter++;

                if (counter % batchSize == 0)
                    await _db.SaveChangesAsync();
            }

            await _db.SaveChangesAsync();
        }
        finally
        {
            _db.ChangeTracker.AutoDetectChangesEnabled = true;
        }

        return result;
    }

    private static InventoryItemResponse MapToResponse(InventoryItem item) => new()
    {
        Id = item.Id,
        PartNumberId = item.PartNumberId,
        PartNumberName = item.PartNumber?.Name ?? "",
        Description = item.Description,
        Qty = item.Qty,
        CompanyId = item.CompanyId,
        CompanyName = item.Company?.Name ?? "",
        Condition = item.Condition,
        Price = item.Price,
        SerialNumber = item.SerialNumber,
        CreatedAt = item.CreatedAt
    };
}
