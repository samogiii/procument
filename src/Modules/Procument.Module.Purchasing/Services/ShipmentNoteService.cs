using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Entities;
using Procument.Module.RFQ.Entities;

namespace Procument.Module.Purchasing.Services;

public interface IShipmentNoteService
{
    Task<List<ShipmentNoteResponse>> GetAllAsync(long? warehouseId = null, IEnumerable<long>? allowedWarehouseIds = null, bool expertView = false);
    Task<ShipmentNoteResponse?> GetByIdAsync(long id);
    Task<ShipmentNoteResponse> CreateAsync(long createdByUserId, CreateShipmentNoteRequest request);
    Task<ShipmentNoteResponse?> UpdateAsync(long id, UpdateShipmentNoteRequest request);
    Task<ShipmentNoteResponse?> UploadPdfAsync(long id, IFormFile file);
    Task<(Stream stream, string fileName, string mimeType)?> DownloadPdfAsync(long id);
    Task<ShipmentNoteResponse?> AddTrackNumberAsync(long snId, long trackNumberId);
    Task<bool> RemoveTrackNumberAsync(long snId, long trackNumberId);
    Task<bool> ConfirmAsync(long id);

    /// <summary>Inventory users update only the AWB / carrier tracking number.</summary>
    Task<ShipmentNoteResponse?> UpdateAwbAsync(long id, UpdateAwbRequest request);

    // Box management
    Task<SnBoxResponse> AddBoxAsync(long snId, SaveSnBoxRequest request);
    Task<SnBoxResponse?> UpdateBoxAsync(long snId, long boxId, SaveSnBoxRequest request);
    Task<bool> DeleteBoxAsync(long snId, long boxId);

    // Customs file upload → triggers Clearing Customs (→ auto Delivered to Customer for CPT)
    Task<ShipmentNoteResponse?> UploadCustomsFileAsync(long id, IFormFile file);

    // Admin manual status update (Received in Office → Delivered to Customer for DDP)
    Task<ShipmentNoteResponse?> UpdateStatusAsync(long id, string status);
}

public class ShipmentNoteService : IShipmentNoteService
{
    private readonly DbContext _db;
    private const string PdfRoot = "Documents/ShipmentNotes";

    public ShipmentNoteService(DbContext db) => _db = db;

    // ── List ───────────────────────────────────────────────────────────────

    private static readonly string[] ExpertVisibleStatuses = [
        "Ship To USA", "Clearing Customs", "Received in Office", "Delivered to Customer"
    ];

    public async Task<List<ShipmentNoteResponse>> GetAllAsync(long? warehouseId = null, IEnumerable<long>? allowedWarehouseIds = null, bool expertView = false)
    {
        var query = BuildDetailQuery();

        // Inventory users are hard-restricted to their assigned warehouses
        if (allowedWarehouseIds != null)
        {
            var allowed = allowedWarehouseIds.ToList();
            query = query.Where(s => allowed.Contains(s.WarehouseId));
        }
        else if (warehouseId.HasValue)
        {
            query = query.Where(s => s.WarehouseId == warehouseId.Value);
        }

        // Expert (SYD) only sees SN#s that are at or past "Ship To USA"
        if (expertView)
            query = query.Where(s => ExpertVisibleStatuses.Contains(s.Status));

        var notes = await query.OrderByDescending(s => s.CreatedAt).ToListAsync();
        return notes.Select(MapResponse).ToList();
    }

    // ── Get by ID ──────────────────────────────────────────────────────────

    public async Task<ShipmentNoteResponse?> GetByIdAsync(long id)
    {
        var note = await BuildDetailQuery().FirstOrDefaultAsync(s => s.Id == id);
        return note == null ? null : MapResponse(note);
    }

    // ── Create ─────────────────────────────────────────────────────────────

    public async Task<ShipmentNoteResponse> CreateAsync(long createdByUserId, CreateShipmentNoteRequest request)
    {
        var type = string.IsNullOrWhiteSpace(request.Type) ? "DDP" : request.Type;
        var itemIds = request.Items.Select(i => i.TrackNumberItemId).ToList();

        // CPT validation: all selected items must belong to a single customer
        if (type == "CPT" && itemIds.Any())
        {
            var customerIds = await (
                from item in _db.Set<TrackNumberItem>()
                join poItem in _db.Set<POItem>() on item.POItemId equals poItem.Id
                join procItem in _db.Set<ProcurementItem>() on (long?)poItem.SourceProcurementItemId equals (long?)procItem.Id into pi
                from procItem in pi.DefaultIfEmpty()
                join rfq in _db.Set<RFQHeader>() on (long?)procItem.SourceRfqId equals (long?)rfq.Id into rfqs
                from rfq in rfqs.DefaultIfEmpty()
                where itemIds.Contains(item.Id)
                select (long?)rfq.CustomerId
            ).Distinct().ToListAsync();

            if (customerIds.Where(id => id != null).Distinct().Count() > 1)
                throw new InvalidOperationException("CPT shipment notes must contain items for a single customer only.");
        }

        // Auto-generate SN number: SN-{yyyy}-{seq:000}
        var year = DateTime.UtcNow.Year;
        var prefix = $"SN-{year}-";
        var existingCount = await _db.Set<ShipmentNote>()
            .CountAsync(s => s.SNNumber.StartsWith(prefix));
        var snNumber = $"{prefix}{(existingCount + 1):000}";

        var note = new ShipmentNote
        {
            SNNumber = snNumber,
            WarehouseId = request.WarehouseId,
            Type = type,
            TId = request.TId,
            SONumber = request.SONumber,
            Destination = request.Destination,
            AWBNumber = request.AWBNumber,
            Status = "Draft",
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = createdByUserId,
        };
        _db.Set<ShipmentNote>().Add(note);
        await _db.SaveChangesAsync();

        // Save CertNeeded per item
        if (itemIds.Any())
        {
            var certMap = request.Items.ToDictionary(i => i.TrackNumberItemId, i => i.CertNeeded);
            var trackItems = await _db.Set<TrackNumberItem>()
                .Where(i => itemIds.Contains(i.Id))
                .ToListAsync();
            foreach (var ti in trackItems)
            {
                if (certMap.TryGetValue(ti.Id, out var cert))
                    ti.CertNeeded = cert;
            }
            await _db.SaveChangesAsync();
        }

        // Resolve distinct TrackNumber IDs from the selected TrackNumberItem IDs
        if (itemIds.Any())
        {
            var trackIds = await _db.Set<TrackNumberItem>()
                .Where(i => itemIds.Contains(i.Id))
                .Select(i => i.TrackNumberId)
                .Distinct()
                .ToListAsync();

            foreach (var trackId in trackIds)
            {
                _db.Set<ShipmentNoteTrackNumber>().Add(new ShipmentNoteTrackNumber
                {
                    ShipmentNoteId = note.Id,
                    TrackNumberId = trackId,
                });

                // Advance track status to "Waiting for Packing"
                var track = await _db.Set<POItemTrackNumber>().FindAsync(trackId);
                if (track != null && track.Status == "Received in Warehouse")
                    track.Status = "Waiting for Packing";
            }

            // Advance SN# itself to "Waiting for Packing" now that tracks are linked
            note.Status = "Waiting for Packing";
            await _db.SaveChangesAsync();
        }

        return (await GetByIdAsync(note.Id))!;
    }

    // ── Update ─────────────────────────────────────────────────────────────

    public async Task<ShipmentNoteResponse?> UpdateAsync(long id, UpdateShipmentNoteRequest request)
    {
        var note = await _db.Set<ShipmentNote>().FindAsync(id);
        if (note == null) return null;

        if (request.TId != null) note.TId = request.TId;
        if (request.SONumber != null) note.SONumber = request.SONumber;
        if (request.Destination != null) note.Destination = request.Destination;
        if (request.AWBNumber != null) note.AWBNumber = request.AWBNumber;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    // ── Upload PDF ─────────────────────────────────────────────────────────

    public async Task<ShipmentNoteResponse?> UploadPdfAsync(long id, IFormFile file)
    {
        var note = await _db.Set<ShipmentNote>().FindAsync(id);
        if (note == null) return null;

        var folder = Path.Combine(PdfRoot, id.ToString());
        Directory.CreateDirectory(folder);

        // Remove old file if present
        if (!string.IsNullOrEmpty(note.PdfFileName))
        {
            var oldPath = Path.Combine(folder, note.PdfFileName);
            if (File.Exists(oldPath)) File.Delete(oldPath);
        }

        var ext = Path.GetExtension(file.FileName);
        var storedName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(folder, storedName);

        await using var fs = File.Create(fullPath);
        await file.CopyToAsync(fs);

        note.PdfFileName = storedName;
        await _db.SaveChangesAsync();

        return await GetByIdAsync(id);
    }

    // ── Download PDF ───────────────────────────────────────────────────────

    public async Task<(Stream stream, string fileName, string mimeType)?> DownloadPdfAsync(long id)
    {
        var note = await _db.Set<ShipmentNote>().AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (note == null || string.IsNullOrEmpty(note.PdfFileName)) return null;

        var path = Path.Combine(PdfRoot, id.ToString(), note.PdfFileName);
        if (!File.Exists(path)) return null;

        return (File.OpenRead(path), $"{note.SNNumber}.pdf", "application/pdf");
    }

    // ── Add / Remove Track Numbers ─────────────────────────────────────────

    public async Task<ShipmentNoteResponse?> AddTrackNumberAsync(long snId, long trackNumberId)
    {
        var note = await _db.Set<ShipmentNote>().FindAsync(snId);
        if (note == null) return null;

        var exists = await _db.Set<ShipmentNoteTrackNumber>()
            .AnyAsync(t => t.ShipmentNoteId == snId && t.TrackNumberId == trackNumberId);

        if (!exists)
        {
            _db.Set<ShipmentNoteTrackNumber>().Add(new ShipmentNoteTrackNumber
            {
                ShipmentNoteId = snId,
                TrackNumberId = trackNumberId,
            });
            await _db.SaveChangesAsync();
        }

        return await GetByIdAsync(snId);
    }

    public async Task<bool> RemoveTrackNumberAsync(long snId, long trackNumberId)
    {
        var link = await _db.Set<ShipmentNoteTrackNumber>()
            .FirstOrDefaultAsync(t => t.ShipmentNoteId == snId && t.TrackNumberId == trackNumberId);
        if (link == null) return false;

        _db.Set<ShipmentNoteTrackNumber>().Remove(link);
        await _db.SaveChangesAsync();
        return true;
    }

    // ── Update AWB (Inventory) ─────────────────────────────────────────────

    public async Task<ShipmentNoteResponse?> UpdateAwbAsync(long id, UpdateAwbRequest request)
    {
        var note = await _db.Set<ShipmentNote>().FindAsync(id);
        if (note == null) return null;

        note.AWBNumber = request.AWBNumber;
        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    // ── Confirm ────────────────────────────────────────────────────────────

    public async Task<bool> ConfirmAsync(long id)
    {
        var note = await _db.Set<ShipmentNote>().FindAsync(id);
        if (note == null) return false;
        note.Status = "Confirmed";
        await _db.SaveChangesAsync();
        return true;
    }

    // ── Box management ─────────────────────────────────────────────────────

    public async Task<SnBoxResponse> AddBoxAsync(long snId, SaveSnBoxRequest request)
    {
        var box = new ShipmentNoteBox
        {
            ShipmentNoteId = snId,
            BoxNumber = request.BoxNumber,
            TrackNumberId = request.TrackNumberId,
            WeightKg = request.WeightKg,
            HeightCm = request.HeightCm,
            WidthCm = request.WidthCm,
            LengthCm = request.LengthCm,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow,
        };
        _db.Set<ShipmentNoteBox>().Add(box);

        // Auto-advance: first box added → Ship To USA (cascades to linked track numbers)
        var note = await _db.Set<ShipmentNote>().FindAsync(snId);
        if (note != null && note.Status == "Waiting for Packing")
        {
            note.Status = "Ship To USA";
            var trackIds = await _db.Set<ShipmentNoteTrackNumber>()
                .Where(t => t.ShipmentNoteId == snId)
                .Select(t => t.TrackNumberId)
                .ToListAsync();
            var tracks = await _db.Set<POItemTrackNumber>()
                .Where(t => trackIds.Contains(t.Id))
                .ToListAsync();
            foreach (var track in tracks)
                if (track.Status == "Waiting for Packing")
                    track.Status = "Ship To USA";
        }

        await _db.SaveChangesAsync();
        return MapSnBoxResponse(box);
    }

    public async Task<SnBoxResponse?> UpdateBoxAsync(long snId, long boxId, SaveSnBoxRequest request)
    {
        var box = await _db.Set<ShipmentNoteBox>().FirstOrDefaultAsync(b => b.Id == boxId && b.ShipmentNoteId == snId);
        if (box == null) return null;
        box.BoxNumber = request.BoxNumber;
        box.TrackNumberId = request.TrackNumberId;
        box.WeightKg = request.WeightKg;
        box.HeightCm = request.HeightCm;
        box.WidthCm = request.WidthCm;
        box.LengthCm = request.LengthCm;
        box.Notes = request.Notes;
        await _db.SaveChangesAsync();
        return MapSnBoxResponse(box);
    }

    public async Task<bool> DeleteBoxAsync(long snId, long boxId)
    {
        var box = await _db.Set<ShipmentNoteBox>().FirstOrDefaultAsync(b => b.Id == boxId && b.ShipmentNoteId == snId);
        if (box == null) return false;
        _db.Set<ShipmentNoteBox>().Remove(box);
        await _db.SaveChangesAsync();
        return true;
    }

    // ── Customs file upload ────────────────────────────────────────────────

    public async Task<ShipmentNoteResponse?> UploadCustomsFileAsync(long id, IFormFile file)
    {
        var note = await _db.Set<ShipmentNote>().FindAsync(id);
        if (note == null) return null;

        // Save the file for the primary SN#
        var (storedName, originalName, uploadedAt) = await SaveCustomsFile(id, note.CustomsFileName, file);

        note.CustomsFileName = storedName;
        note.CustomsOriginalFileName = originalName;
        note.CustomsUploadedAt = uploadedAt;
        note.Status = note.Type == "CPT" ? "Delivered to Customer" : "Clearing Customs";

        // Propagate to all sibling SN#s sharing the same T-ID (only those still at "Ship To USA")
        if (!string.IsNullOrWhiteSpace(note.TId))
        {
            var siblings = await _db.Set<ShipmentNote>()
                .Where(s => s.TId == note.TId && s.Id != id && s.Status == "Ship To USA")
                .ToListAsync();

            foreach (var sibling in siblings)
            {
                // Copy the physical file into each sibling's folder
                var (sibName, _, _) = await CopyCustomsFile(id, storedName, sibling.Id, sibling.CustomsFileName, originalName);
                sibling.CustomsFileName = sibName;
                sibling.CustomsOriginalFileName = originalName;
                sibling.CustomsUploadedAt = uploadedAt;
                sibling.Status = sibling.Type == "CPT" ? "Delivered to Customer" : "Clearing Customs";
            }
        }

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    private async Task<(string storedName, string originalName, DateTime uploadedAt)> SaveCustomsFile(long snId, string? existingFileName, IFormFile file)
    {
        var folder = Path.Combine(PdfRoot, snId.ToString());
        Directory.CreateDirectory(folder);

        if (!string.IsNullOrEmpty(existingFileName))
        {
            var oldPath = Path.Combine(folder, existingFileName);
            if (File.Exists(oldPath)) File.Delete(oldPath);
        }

        var ext = Path.GetExtension(file.FileName);
        var storedName = $"customs_{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(folder, storedName);

        await using var fs = File.Create(fullPath);
        await file.CopyToAsync(fs);

        return (storedName, file.FileName, DateTime.UtcNow);
    }

    private static Task<(string storedName, string originalName, DateTime uploadedAt)> CopyCustomsFile(
        long sourceSNId, string sourceFileName, long targetSnId, string? existingTargetFileName, string originalFileName)
    {
        var sourceFolder = Path.Combine(PdfRoot, sourceSNId.ToString());
        var sourcePath = Path.Combine(sourceFolder, sourceFileName);

        var targetFolder = Path.Combine(PdfRoot, targetSnId.ToString());
        Directory.CreateDirectory(targetFolder);

        if (!string.IsNullOrEmpty(existingTargetFileName))
        {
            var oldPath = Path.Combine(targetFolder, existingTargetFileName);
            if (File.Exists(oldPath)) File.Delete(oldPath);
        }

        var ext = Path.GetExtension(sourceFileName);
        var newName = $"customs_{Guid.NewGuid():N}{ext}";
        var targetPath = Path.Combine(targetFolder, newName);

        if (File.Exists(sourcePath))
            File.Copy(sourcePath, targetPath, overwrite: true);

        return Task.FromResult((newName, originalFileName, DateTime.UtcNow));
    }

    // ── Admin manual status update ─────────────────────────────────────────

    public async Task<ShipmentNoteResponse?> UpdateStatusAsync(long id, string status)
    {
        var note = await _db.Set<ShipmentNote>().FindAsync(id);
        if (note == null) return null;
        note.Status = status;

        // When advancing to Ship To USA, mark linked tracks accordingly
        if (status == "Ship To USA")
        {
            var trackIds = await _db.Set<ShipmentNoteTrackNumber>()
                .Where(t => t.ShipmentNoteId == id)
                .Select(t => t.TrackNumberId)
                .ToListAsync();
            var tracks = await _db.Set<POItemTrackNumber>()
                .Where(t => trackIds.Contains(t.Id))
                .ToListAsync();
            foreach (var track in tracks)
                if (track.Status == "Waiting for Packing")
                    track.Status = "Ship To USA";
        }

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    // ── Query builder ──────────────────────────────────────────────────────

    private IQueryable<ShipmentNote> BuildDetailQuery() =>
        _db.Set<ShipmentNote>()
            .AsNoTracking()
            .Include(s => s.Warehouse)
            .Include(s => s.CreatedBy)
            .Include(s => s.TrackNumbers)
                .ThenInclude(t => t.TrackNumber)
                    .ThenInclude(tn => tn.POItem)
                        .ThenInclude(p => p!.PartNumber)
            .Include(s => s.TrackNumbers)
                .ThenInclude(t => t.TrackNumber)
                    .ThenInclude(tn => tn.POItem)
                        .ThenInclude(p => p!.PurchaseOrder)
                            .ThenInclude(po => po!.Supplier)
            .Include(s => s.TrackNumbers)
                .ThenInclude(t => t.TrackNumber)
                    .ThenInclude(tn => tn.POItem)
                        .ThenInclude(p => p!.ProcumentRecord)
                            .ThenInclude(pr => pr!.RFQItem)
                                .ThenInclude(ri => ri.RFQ)
                                    .ThenInclude(rfq => rfq.Customer)
            .Include(s => s.TrackNumbers)
                .ThenInclude(t => t.TrackNumber)
                    .ThenInclude(tn => tn.Items)
            .Include(s => s.TrackNumbers)
                .ThenInclude(t => t.TrackNumber)
                    .ThenInclude(tn => tn.Boxes)
            .Include(s => s.Boxes);

    // ── Mapping ────────────────────────────────────────────────────────────

    private static ShipmentNoteResponse MapResponse(ShipmentNote s) => new()
    {
        Id = s.Id,
        SNNumber = s.SNNumber,
        WarehouseId = s.WarehouseId,
        WarehouseName = s.Warehouse?.Name,
        Type = s.Type,
        TId = s.TId,
        SONumber = s.SONumber,
        Destination = s.Destination,
        AWBNumber = s.AWBNumber,
        PdfFileName = s.PdfFileName,
        Status = s.Status,
        CustomsFileName = s.CustomsFileName,
        CustomsOriginalFileName = s.CustomsOriginalFileName,
        CustomsUploadedAt = s.CustomsUploadedAt,
        CreatedAt = s.CreatedAt,
        CreatedByUserId = s.CreatedByUserId,
        CreatedByName = s.CreatedBy?.Name,
        TrackNumbers = s.TrackNumbers?.Select(t => new ShipmentNoteTrackResponse
        {
            TrackNumberId = t.TrackNumberId,
            TrackNumber = t.TrackNumber?.TrackNumber ?? string.Empty,
            Carrier = t.TrackNumber?.Carrier,
            Status = t.TrackNumber?.Status ?? "Active",
            POItemId = t.TrackNumber?.POItemId ?? 0,
            PartNumberName = t.TrackNumber?.POItem?.PartNumber?.Name,
            Description = t.TrackNumber?.POItem?.PartNumber?.Description,
            SupplierName = t.TrackNumber?.POItem?.PurchaseOrder?.Supplier?.Name,
            Qty = t.TrackNumber?.POItem?.Qty ?? 0,
            Condition = t.TrackNumber?.POItem?.Condition,
            CustomerName = t.TrackNumber?.POItem?.ProcumentRecord?.RFQItem?.RFQ?.Customer?.Name,
            CustomerCode = t.TrackNumber?.POItem?.ProcumentRecord?.RFQItem?.RFQ?.Customer?.CustomerCode,
            Items = t.TrackNumber?.Items?.Select(i => new ShipmentNoteItemResponse
            {
                TrackNumberItemId = i.Id,
                POItemId = i.POItemId,
                PartNumberName = t.TrackNumber?.POItem?.PartNumber?.Name,
                ActualQty = i.ActualQty,
                CertNeeded = i.CertNeeded,
                Status = i.Status,
            }).ToList() ?? new(),
            ReceivedBoxes = t.TrackNumber?.Boxes?.OrderBy(b => b.BoxNumber).Select(b => new TrackBoxResponse
            {
                Id = b.Id,
                TrackNumberId = b.TrackNumberId,
                BoxNumber = b.BoxNumber,
                WeightKg = b.WeightKg,
                HeightCm = b.HeightCm,
                WidthCm = b.WidthCm,
                LengthCm = b.LengthCm,
                Notes = b.Notes,
                CreatedAt = b.CreatedAt,
            }).ToList() ?? new(),
        }).ToList() ?? new(),
        Boxes = s.Boxes?.Select(MapSnBoxResponse).ToList() ?? new(),
    };

    private static SnBoxResponse MapSnBoxResponse(ShipmentNoteBox b) => new()
    {
        Id = b.Id,
        ShipmentNoteId = b.ShipmentNoteId,
        BoxNumber = b.BoxNumber,
        TrackNumberId = b.TrackNumberId,
        WeightKg = b.WeightKg,
        HeightCm = b.HeightCm,
        WidthCm = b.WidthCm,
        LengthCm = b.LengthCm,
        Notes = b.Notes,
        CreatedAt = b.CreatedAt,
    };
}
