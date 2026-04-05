using Microsoft.EntityFrameworkCore;
using Procument.Module.Catalog.Entities;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Entities;
using Procument.Module.RFQ.Entities;

namespace Procument.Module.Purchasing.Services;

public interface IILSService
{
    Task<List<ILSItemResponse>> GetAllAsync();
    Task<ILSItemResponse> SaveAsync(SaveILSItemRequest request);
    Task<bool> DeleteAsync(long id);
    Task<List<ARShopSuggestionResponse>> GetARShopSuggestionsAsync();
    Task<BulkImportResult> BulkImportAsync(BulkImportILSRequest request);
}

public class ILSService : IILSService
{
    private readonly DbContext _db;

    public ILSService(DbContext db) => _db = db;

    public async Task<List<ILSItemResponse>> GetAllAsync()
    {
        var items = await _db.Set<ILSItem>()
            .Include(i => i.PartNumber)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        return items.Select(MapToResponse).ToList();
    }

    public async Task<ILSItemResponse> SaveAsync(SaveILSItemRequest request)
    {
        ILSItem item;

        if (request.Id.HasValue && request.Id > 0)
        {
            item = await _db.Set<ILSItem>()
                .FirstOrDefaultAsync(i => i.Id == request.Id.Value)
                ?? throw new KeyNotFoundException($"ILS item {request.Id} not found.");

            item.PartNumberId = request.PartNumberId;
            item.Description = request.Description;
            item.AltPartNumber = request.AltPartNumber;
            item.Price = request.Price;
            item.Qty = request.Qty;
            item.Condition = request.Condition;
            item.TagDate = request.TagDate;
            item.CertName = request.CertName;
            item.LeadTime = request.LeadTime;
            item.ProcumentRecordId = request.ProcumentRecordId;
        }
        else
        {
            item = new ILSItem
            {
                PartNumberId = request.PartNumberId,
                Description = request.Description,
                AltPartNumber = request.AltPartNumber,
                Price = request.Price,
                Qty = request.Qty,
                Condition = request.Condition,
                TagDate = request.TagDate,
                CertName = request.CertName,
                LeadTime = request.LeadTime,
                ProcumentRecordId = request.ProcumentRecordId,
                CreatedAt = DateTime.UtcNow,
            };
            _db.Set<ILSItem>().Add(item);
        }

        await _db.SaveChangesAsync();

        item = await _db.Set<ILSItem>()
            .Include(i => i.PartNumber)
            .FirstAsync(i => i.Id == item.Id);

        return MapToResponse(item);
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var item = await _db.Set<ILSItem>().FindAsync(id);
        if (item == null) return false;
        _db.Set<ILSItem>().Remove(item);
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Returns all AR-condition Shop records (type='Shop') that have a Fix Price,
    /// along with their parent procurement info and part number.
    /// These are shops actively fixing AR parts that can be added to ILS inventory.
    /// </summary>
    public async Task<List<ARShopSuggestionResponse>> GetARShopSuggestionsAsync()
    {
        var shops = await _db.Set<ProcumentRecord>()
            .Include(r => r.Supplier)
            .Include(r => r.RFQItem)
                .ThenInclude(i => i.PartNumber)
            .Include(r => r.RFQItem)
                .ThenInclude(i => i.RFQ)
            .Where(r => (r.Type ?? "Procument") == "Shop" && r.Condition == "AR")
            .OrderByDescending(r => r.Id)
            .ToListAsync();

        return shops.Select(r => new ARShopSuggestionResponse
        {
            ProcumentRecordId = r.Id,
            RFQItemId = r.RFQItemId,
            RFQId = r.RFQItem.RFQId,
            RFQName = r.RFQItem.RFQ.Name,
            PartNumberId = r.RFQItem.PartNumberId,
            PartNumberName = r.RFQItem.PartNumber.Name,
            AltPartNumber = r.Alt,
            SupplierName = r.Supplier.Name,
            Price = r.Price,
            FixPrice = r.FixPrice,
            Qty = r.Qty,
            Condition = r.Condition,
            CertName = r.CertName,
            TagDate = r.TagDate,
            LeadTime = r.LeadTime,
            ShippingCost = r.ShippingCost,
            ShippingPoint = r.ShippingPoint,
        }).ToList();
    }

    public async Task<BulkImportResult> BulkImportAsync(BulkImportILSRequest request)
    {
        var result = new BulkImportResult();
        var allPartNumbers = await _db.Set<PartNumber>().ToListAsync();
        var toAdd = new List<ILSItem>();

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

            DateOnly? tagDate = null;
            if (!string.IsNullOrWhiteSpace(row.TagDate) && DateOnly.TryParse(row.TagDate, out var d))
                tagDate = d;

            toAdd.Add(new ILSItem
            {
                PartNumberId = pn.Id,
                Description = row.Description,
                AltPartNumber = row.AltPartNumber,
                Price = row.Price,
                Qty = row.Qty,
                Condition = row.Condition,
                TagDate = tagDate,
                CertName = row.CertName,
                LeadTime = row.LeadTime,
                CreatedAt = DateTime.UtcNow
            });
            result.Created++;
        }

        if (toAdd.Any())
        {
            _db.Set<ILSItem>().AddRange(toAdd);
            await _db.SaveChangesAsync();
        }

        return result;
    }

    private static ILSItemResponse MapToResponse(ILSItem i) => new()
    {
        Id = i.Id,
        PartNumberId = i.PartNumberId,
        PartNumberName = i.PartNumber.Name,
        Description = i.Description,
        AltPartNumber = i.AltPartNumber,
        Price = i.Price,
        Qty = i.Qty,
        Condition = i.Condition,
        TagDate = i.TagDate,
        CertName = i.CertName,
        LeadTime = i.LeadTime,
        ProcumentRecordId = i.ProcumentRecordId,
        CreatedAt = i.CreatedAt,
    };
}
