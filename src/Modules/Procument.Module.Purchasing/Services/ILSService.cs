using Microsoft.EntityFrameworkCore;
using Procument.Module.Catalog.Entities;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Entities;
using Procument.Module.RFQ.Entities;

namespace Procument.Module.Purchasing.Services;

public interface IILSService
{
    Task<List<ILSItemResponse>> GetAllAsync();
    Task<ILSItemResponse?> GetByIdAsync(long id);
    Task<ILSItemResponse> SaveAsync(SaveILSItemRequest request);
    Task<bool> DeleteAsync(long id);
    Task<List<ARShopSuggestionResponse>> GetARShopSuggestionsAsync();
    Task<BulkImportResult> BulkImportAsync(BulkImportILSRequest request);

    // ── Serials ──
    Task<List<ILSSerialResponse>> GetSerialsAsync(long ilsItemId);
    Task<ILSSerialResponse> SaveSerialAsync(SaveILSSerialRequest request);
    Task<bool> DeleteSerialAsync(long serialId);
    Task<ILSSerialResponse?> UploadSerialImageAsync(long serialId, string kind, Microsoft.AspNetCore.Http.IFormFile file);
    Task<(Stream stream, string fileName, string mimeType)?> GetSerialImageAsync(long serialId, string kind);
    Task<ILSSerialResponse?> DeleteSerialImageAsync(long serialId, string kind);
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

        var counts = await _db.Set<ILSItemSerial>()
            .GroupBy(s => s.ILSItemId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        return items.Select(i => MapToResponse(i, counts.GetValueOrDefault(i.Id))).ToList();
    }

    public async Task<ILSItemResponse?> GetByIdAsync(long id)
    {
        var item = await _db.Set<ILSItem>()
            .Include(i => i.PartNumber)
            .FirstOrDefaultAsync(i => i.Id == id);
        if (item == null) return null;

        var count = await _db.Set<ILSItemSerial>().CountAsync(s => s.ILSItemId == id);
        return MapToResponse(item, count);
    }

    public async Task<ILSItemResponse> SaveAsync(SaveILSItemRequest request)
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
            var existing = await _db.Set<PartNumber>()
                .FirstOrDefaultAsync(p => p.Name == trimmed);
            if (existing != null)
            {
                partNumberId = existing.Id;
            }
            else
            {
                var newPn = new PartNumber
                {
                    Name = trimmed,
                    Description = request.Description,
                    CreatedAt = DateTime.UtcNow
                };
                _db.Set<PartNumber>().Add(newPn);
                await _db.SaveChangesAsync();
                partNumberId = newPn.Id;
            }
        }
        else
        {
            throw new ArgumentException("PartNumberId or PartNumberName is required");
        }

        ILSItem item;

        if (request.Id.HasValue && request.Id > 0)
        {
            item = await _db.Set<ILSItem>()
                .FirstOrDefaultAsync(i => i.Id == request.Id.Value)
                ?? throw new KeyNotFoundException($"ILS item {request.Id} not found.");

            item.PartNumberId = partNumberId;
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
                PartNumberId = partNumberId,
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
            .Where(r => (r.Type ?? "Procument") == "Shop")
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
        if (request.Rows == null || !request.Rows.Any()) return result;

        _db.ChangeTracker.AutoDetectChangesEnabled = false;

        try
        {
            var requestedPnNames = request.Rows
                .Select(r => r.PartNumberName?.Trim())
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var existingPns = await _db.Set<PartNumber>()
                .Where(p => requestedPnNames.Contains(p.Name))
                .ToDictionaryAsync(p => p.Name.ToLower(), p => p);

            var newPns = new Dictionary<string, PartNumber>();

            int batchSize = 1000;
            int counter = 0;

            foreach (var row in request.Rows)
            {
                var pnName = row.PartNumberName?.Trim();
                if (string.IsNullOrWhiteSpace(pnName))
                {
                    result.Skipped++;
                    continue;
                }

                var pnLower = pnName.ToLower();

                if (!existingPns.TryGetValue(pnLower, out var pn) && !newPns.TryGetValue(pnLower, out pn))
                {
                    pn = new PartNumber
                    {
                        Name = pnName,
                        Description = row.Description,
                        CreatedAt = DateTime.UtcNow
                    };
                    newPns[pnLower] = pn;
                    _db.Set<PartNumber>().Add(pn);
                }

                DateOnly? tagDate = null;
                if (!string.IsNullOrWhiteSpace(row.TagDate) && DateOnly.TryParse(row.TagDate, out var d))
                    tagDate = d;

                _db.Set<ILSItem>().Add(new ILSItem
                {
                    PartNumber = pn,
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

    private static ILSItemResponse MapToResponse(ILSItem i, int serialCount = 0) => new()
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
        SerialCount = serialCount,
        CreatedAt = i.CreatedAt,
    };

    // ════════════════════════════════════════════════════════════
    // SERIALS — one physical unit per row; parent Qty auto-syncs
    // ════════════════════════════════════════════════════════════

    private const string SerialDocRoot = "Documents/ILSSerials";

    public async Task<List<ILSSerialResponse>> GetSerialsAsync(long ilsItemId)
    {
        var serials = await _db.Set<ILSItemSerial>()
            .Where(s => s.ILSItemId == ilsItemId)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync();

        return serials.Select(MapSerial).ToList();
    }

    public async Task<ILSSerialResponse> SaveSerialAsync(SaveILSSerialRequest request)
    {
        var itemExists = await _db.Set<ILSItem>().AnyAsync(i => i.Id == request.ILSItemId);
        if (!itemExists)
            throw new KeyNotFoundException($"ILS item {request.ILSItemId} not found.");

        if (string.IsNullOrWhiteSpace(request.SerialNumber))
            throw new ArgumentException("Serial number is required.");

        ILSItemSerial serial;
        if (request.Id.HasValue && request.Id > 0)
        {
            serial = await _db.Set<ILSItemSerial>()
                .FirstOrDefaultAsync(s => s.Id == request.Id.Value)
                ?? throw new KeyNotFoundException($"Serial {request.Id} not found.");

            serial.SerialNumber = request.SerialNumber.Trim();
            serial.LeadTime = request.LeadTime;
            serial.CertText = request.CertText;
            serial.Price = request.Price;
            serial.Location = request.Location;
            serial.Condition = request.Condition;
            serial.TagDate = request.TagDate;
            serial.Notes = request.Notes;
        }
        else
        {
            serial = new ILSItemSerial
            {
                ILSItemId = request.ILSItemId,
                SerialNumber = request.SerialNumber.Trim(),
                LeadTime = request.LeadTime,
                CertText = request.CertText,
                Price = request.Price,
                Location = request.Location,
                Condition = request.Condition,
                TagDate = request.TagDate,
                Notes = request.Notes,
                CreatedAt = DateTime.UtcNow,
            };
            _db.Set<ILSItemSerial>().Add(serial);
        }

        await _db.SaveChangesAsync();
        await RecomputeQtyAsync(serial.ILSItemId);

        return MapSerial(serial);
    }

    public async Task<bool> DeleteSerialAsync(long serialId)
    {
        var serial = await _db.Set<ILSItemSerial>().FindAsync(serialId);
        if (serial == null) return false;

        var ilsItemId = serial.ILSItemId;
        _db.Set<ILSItemSerial>().Remove(serial);
        await _db.SaveChangesAsync();

        // Remove stored image files for this serial
        var folder = Path.Combine(SerialDocRoot, serialId.ToString());
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);

        await RecomputeQtyAsync(ilsItemId);
        return true;
    }

    public async Task<ILSSerialResponse?> UploadSerialImageAsync(long serialId, string kind, Microsoft.AspNetCore.Http.IFormFile file)
    {
        var serial = await _db.Set<ILSItemSerial>().FindAsync(serialId);
        if (serial == null) return null;

        var prefix = NormalizeKind(kind);
        var folder = Path.Combine(SerialDocRoot, serialId.ToString());
        Directory.CreateDirectory(folder);

        // Remove the old file of this kind if present
        var oldStored = prefix == "cert" ? serial.CertImageFileName : serial.PartImageFileName;
        if (!string.IsNullOrEmpty(oldStored))
        {
            var oldPath = Path.Combine(folder, oldStored);
            if (File.Exists(oldPath)) File.Delete(oldPath);
        }

        var ext = Path.GetExtension(file.FileName);
        var storedName = $"{prefix}_{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(folder, storedName);

        await using (var fs = File.Create(fullPath))
            await file.CopyToAsync(fs);

        if (prefix == "cert")
        {
            serial.CertImageFileName = storedName;
            serial.CertImageOriginalName = file.FileName;
        }
        else
        {
            serial.PartImageFileName = storedName;
            serial.PartImageOriginalName = file.FileName;
        }

        await _db.SaveChangesAsync();
        return MapSerial(serial);
    }

    public async Task<(Stream stream, string fileName, string mimeType)?> GetSerialImageAsync(long serialId, string kind)
    {
        var serial = await _db.Set<ILSItemSerial>().AsNoTracking().FirstOrDefaultAsync(s => s.Id == serialId);
        if (serial == null) return null;

        var prefix = NormalizeKind(kind);
        var storedName = prefix == "cert" ? serial.CertImageFileName : serial.PartImageFileName;
        var originalName = prefix == "cert" ? serial.CertImageOriginalName : serial.PartImageOriginalName;
        if (string.IsNullOrEmpty(storedName)) return null;

        var path = Path.Combine(SerialDocRoot, serialId.ToString(), storedName);
        if (!File.Exists(path)) return null;

        var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(path, out var mime)) mime = "application/octet-stream";

        return (File.OpenRead(path), originalName ?? storedName, mime);
    }

    public async Task<ILSSerialResponse?> DeleteSerialImageAsync(long serialId, string kind)
    {
        var serial = await _db.Set<ILSItemSerial>().FindAsync(serialId);
        if (serial == null) return null;

        var prefix = NormalizeKind(kind);
        var storedName = prefix == "cert" ? serial.CertImageFileName : serial.PartImageFileName;
        if (!string.IsNullOrEmpty(storedName))
        {
            var path = Path.Combine(SerialDocRoot, serialId.ToString(), storedName);
            if (File.Exists(path)) File.Delete(path);
        }

        if (prefix == "cert")
        {
            serial.CertImageFileName = null;
            serial.CertImageOriginalName = null;
        }
        else
        {
            serial.PartImageFileName = null;
            serial.PartImageOriginalName = null;
        }

        await _db.SaveChangesAsync();
        return MapSerial(serial);
    }

    private async Task RecomputeQtyAsync(long ilsItemId)
    {
        var item = await _db.Set<ILSItem>().FirstOrDefaultAsync(i => i.Id == ilsItemId);
        if (item == null) return;
        item.Qty = await _db.Set<ILSItemSerial>().CountAsync(s => s.ILSItemId == ilsItemId);
        await _db.SaveChangesAsync();
    }

    private static string NormalizeKind(string kind)
    {
        var k = (kind ?? "").Trim().ToLowerInvariant();
        if (k != "cert" && k != "part")
            throw new ArgumentException("Image kind must be 'cert' or 'part'.");
        return k;
    }

    private static ILSSerialResponse MapSerial(ILSItemSerial s) => new()
    {
        Id = s.Id,
        ILSItemId = s.ILSItemId,
        SerialNumber = s.SerialNumber,
        LeadTime = s.LeadTime,
        CertText = s.CertText,
        HasCertImage = !string.IsNullOrEmpty(s.CertImageFileName),
        CertImageOriginalName = s.CertImageOriginalName,
        HasPartImage = !string.IsNullOrEmpty(s.PartImageFileName),
        PartImageOriginalName = s.PartImageOriginalName,
        Price = s.Price,
        Location = s.Location,
        Condition = s.Condition,
        TagDate = s.TagDate,
        Notes = s.Notes,
        CreatedAt = s.CreatedAt,
    };
}
