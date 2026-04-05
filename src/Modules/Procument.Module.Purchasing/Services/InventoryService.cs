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
        InventoryItem item;

        if (request.Id.HasValue && request.Id.Value > 0)
        {
            item = await _db.Set<InventoryItem>().FindAsync(request.Id.Value)
                ?? throw new InvalidOperationException($"InventoryItem {request.Id} not found");

            item.PartNumberId = request.PartNumberId;
            item.Description = request.Description;
            item.Qty = request.Qty;
            item.CompanyId = request.CompanyId;
            item.Condition = request.Condition;
            item.Price = request.Price;
        }
        else
        {
            item = new InventoryItem
            {
                PartNumberId = request.PartNumberId,
                Description = request.Description,
                Qty = request.Qty,
                CompanyId = request.CompanyId,
                Condition = request.Condition,
                Price = request.Price,
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

        var allPartNumbers = await _db.Set<Procument.Module.Catalog.Entities.PartNumber>()
            .ToListAsync();
        var allSuppliers = await _db.Set<Procument.Module.Catalog.Entities.Supplier>()
            .ToListAsync();

        var toAdd = new List<InventoryItem>();

        foreach (var row in request.Rows)
        {
            var pn = allPartNumbers.FirstOrDefault(p =>
                p.Name.Equals(row.PartNumberName.Trim(), StringComparison.OrdinalIgnoreCase));

            if (pn == null)
            {
                result.Skipped++;
                result.Errors.Add($"PartNumber '{row.PartNumberName}' not found");
                continue;
            }

            long companyId = 0;
            if (!string.IsNullOrWhiteSpace(row.CompanyName))
            {
                var supplier = allSuppliers.FirstOrDefault(s =>
                    s.Name.Equals(row.CompanyName.Trim(), StringComparison.OrdinalIgnoreCase));
                if (supplier == null)
                {
                    result.Skipped++;
                    result.Errors.Add($"Company '{row.CompanyName}' not found");
                    continue;
                }
                companyId = supplier.Id;
            }
            else
            {
                result.Skipped++;
                result.Errors.Add($"Company is required for row with PartNumber '{row.PartNumberName}'");
                continue;
            }

            toAdd.Add(new InventoryItem
            {
                PartNumberId = pn.Id,
                Description = row.Description,
                Qty = row.Qty,
                CompanyId = companyId,
                Condition = row.Condition,
                Price = row.Price,
                CreatedAt = DateTime.UtcNow
            });
            result.Created++;
        }

        if (toAdd.Any())
        {
            _db.Set<InventoryItem>().AddRange(toAdd);
            await _db.SaveChangesAsync();
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
        CreatedAt = item.CreatedAt
    };
}
