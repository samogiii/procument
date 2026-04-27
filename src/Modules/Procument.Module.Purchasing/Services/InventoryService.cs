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
    Task<List<InventoryItemResponse>> BulkSearchInventory(BulkSearch search);
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
    public async Task<List<InventoryItemResponse>> BulkSearchInventory(BulkSearch search)
    {
        // 1. Start building the query
        var query = _db.Set<InventoryItem>()
            .Include(i => i.PartNumber)
            .Include(i => i.Company)
            .AsQueryable();

        // 2. Apply the IN clause filter safely
        if (search?.Names != null && search.Names.Any())
        {
            query = query.Where(i => search.Names.Contains(i.PartNumber.Name));
        }

        // 3. Execute the query and fetch the results from the database
        var items = await query
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        // 4. Map the database entities to your response DTOs
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

        // 1. Group and Aggregate incoming rows
        // Key: PN Name | Company Name | Serial Number | Condition | Description
        var groupedRows = request.Rows
            .Where(r => !string.IsNullOrWhiteSpace(r.PartNumberName) && !string.IsNullOrWhiteSpace(r.CompanyName))
            .GroupBy(r => new
            {
                PnName = r.PartNumberName.Trim().ToLower(),
                CompName = r.CompanyName.Trim().ToLower(),
                SN = (r.SerialNumber ?? "").Trim().ToLower(),
                Cond = (r.Condition ?? "").Trim().ToLower(),
                Desc = (r.Description ?? "").Trim().ToLower()
            })
            .Select(g => new
            {
                Key = g.Key,
                OriginalPnName = g.First().PartNumberName.Trim(),
                OriginalCompName = g.First().CompanyName.Trim(),
                TotalQty = g.Sum(x => x.Qty),
                Price = g.First().Price, // Take price from first occurrence
                OriginalDescription = g.First().Description,
                OriginalCondition = g.First().Condition,
                OriginalSerialNumber = g.First().SerialNumber
            })
            .ToList();

        result.Skipped = request.Rows.Count - groupedRows.Count; // Initial skip based on missing required fields or grouping

        _db.ChangeTracker.AutoDetectChangesEnabled = false;

        try
        {
            // 2. Pre-resolve PartNumbers and Suppliers
            var pnNames = groupedRows.Select(r => r.Key.PnName).Distinct().ToList();
            var compNames = groupedRows.Select(r => r.Key.CompName).Distinct().ToList();

            var existingPns = await _db.Set<Procument.Module.Catalog.Entities.PartNumber>()
                .Where(p => pnNames.Contains(p.Name.ToLower()))
                .ToDictionaryAsync(p => p.Name.ToLower(), p => p);

            var existingSuppliers = await _db.Set<Procument.Module.Catalog.Entities.Supplier>()
                .Where(s => compNames.Contains(s.Name.ToLower()))
                .ToDictionaryAsync(s => s.Name.ToLower(), s => s);

            var newPns = new Dictionary<string, Procument.Module.Catalog.Entities.PartNumber>();
            var newSuppliers = new Dictionary<string, Procument.Module.Catalog.Entities.Supplier>();

            // 3. Pre-fetch existing inventory items to check for updates
            // We only fetch items that match the PNs and Suppliers in the import
            var supplierIds = existingSuppliers.Values.Select(s => s.Id).ToList();
            var pnIds = existingPns.Values.Select(p => p.Id).ToList();

            var existingInventory = await _db.Set<InventoryItem>()
                .Where(i => pnIds.Contains(i.PartNumberId) && supplierIds.Contains(i.CompanyId))
                .ToListAsync();

            // Index existing inventory for fast lookup
            // Key: PNId | CompanyId | SN | Cond | Desc (normalized)
            // Use a loop instead of ToDictionary to handle cases where the DB already has duplicates
            var inventoryLookup = new Dictionary<string, InventoryItem>();
            foreach (var item in existingInventory)
            {
                var key = $"{item.PartNumberId}|{item.CompanyId}|{(item.SerialNumber ?? "").Trim().ToLower()}|{(item.Condition ?? "").Trim().ToLower()}|{(item.Description ?? "").Trim().ToLower()}";
                if (!inventoryLookup.ContainsKey(key))
                {
                    inventoryLookup[key] = item;
                }
            }

            foreach (var group in groupedRows)
            {
                // Resolve or Create PartNumber
                if (!existingPns.TryGetValue(group.Key.PnName, out var pn) && !newPns.TryGetValue(group.Key.PnName, out pn))
                {
                    pn = new Procument.Module.Catalog.Entities.PartNumber
                    {
                        Name = group.OriginalPnName,
                        Description = group.OriginalDescription,
                        CreatedAt = DateTime.UtcNow
                    };
                    newPns[group.Key.PnName] = pn;
                    _db.Set<Procument.Module.Catalog.Entities.PartNumber>().Add(pn);
                    await _db.SaveChangesAsync(); // Need ID for lookup/assignment
                }

                // Resolve or Create Supplier
                if (!existingSuppliers.TryGetValue(group.Key.CompName, out var supplier) && !newSuppliers.TryGetValue(group.Key.CompName, out supplier))
                {
                    supplier = new Procument.Module.Catalog.Entities.Supplier
                    {
                        Name = group.OriginalCompName,
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true,
                        Status = "Approved"
                    };
                    newSuppliers[group.Key.CompName] = supplier;
                    _db.Set<Procument.Module.Catalog.Entities.Supplier>().Add(supplier);
                    await _db.SaveChangesAsync(); // Need ID
                }

                var lookupKey = $"{pn.Id}|{supplier.Id}|{group.Key.SN}|{group.Key.Cond}|{group.Key.Desc}";

                if (inventoryLookup.TryGetValue(lookupKey, out var existingItem))
                {
                    // Aggregation: Increase QTY
                    existingItem.Qty += group.TotalQty;
                    _db.Set<InventoryItem>().Update(existingItem);
                    result.Updated++;
                }
                else
                {
                    // Create New
                    var newItem = new InventoryItem
                    {
                        PartNumberId = pn.Id,
                        CompanyId = supplier.Id,
                        Description = group.OriginalDescription,
                        Qty = group.TotalQty,
                        Condition = group.OriginalCondition,
                        Price = group.Price,
                        SerialNumber = group.OriginalSerialNumber,
                        CreatedAt = DateTime.UtcNow
                    };
                    _db.Set<InventoryItem>().Add(newItem);
                    inventoryLookup[lookupKey] = newItem; // Add to lookup so if another group in the batch matches, it aggregates
                    result.Created++;
                }
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
