using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Entities;
using Procument.Module.RFQ.Entities;

namespace Procument.Module.Purchasing.Services;

public interface IShipmentNoteService
{
    /// <param name="allowedBases">Customer bases the user may see (Menu Access); null = no filter.</param>
    Task<List<ShipmentNoteResponse>> GetAllAsync(long? warehouseId = null, IEnumerable<long>? allowedWarehouseIds = null, bool expertView = false, IReadOnlySet<int>? allowedBases = null);
    Task<ShipmentNoteResponse?> GetByIdAsync(long id);
    Task<ShipmentNoteResponse> CreateAsync(long createdByUserId, CreateShipmentNoteRequest request);

    /// <summary>
    /// PickUp (Base 2–5): the customer collected the parts at the warehouse. Creates a finished
    /// note (Type "PickUp", Status "Confirmed") with the customer's documents attached.
    /// </summary>
    Task<ShipmentNoteResponse> CreatePickupAsync(long createdByUserId, CreatePickupRequest request, IReadOnlyList<IFormFile> files, IReadOnlySet<int>? allowedBases);
    Task<ShipmentNoteResponse?> AddDocumentsAsync(long id, long userId, IReadOnlyList<IFormFile> files);
    Task<(Stream stream, string fileName, string mimeType)?> DownloadDocumentAsync(long id, long docId);
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

    public async Task<List<ShipmentNoteResponse>> GetAllAsync(long? warehouseId = null, IEnumerable<long>? allowedWarehouseIds = null, bool expertView = false, IReadOnlySet<int>? allowedBases = null)
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
        var result = notes.Select(MapResponse).ToList();
        var bases = await ShippingCustomerBases.LoadAsync(_db,
            result.SelectMany(n => n.TrackNumbers).Select(t => t.POItemId));
        FillCustomerBases(result, bases);

        // A note is shown when any of its parts is in one of the user's bases. The base is only
        // known after the lookup above, so this runs in memory (the list is not paged).
        if (allowedBases != null)
            result = result.Where(n => n.TrackNumbers.Count == 0
                ? allowedBases.Contains(ShippingBaseAccess.Effective(null))
                : n.TrackNumbers.Any(t => allowedBases.Contains(ShippingBaseAccess.Effective(t.CustomerBase)))).ToList();
        return result;
    }

    private static void FillCustomerBases(IEnumerable<ShipmentNoteResponse> notes, Dictionary<long, int?> bases)
    {
        foreach (var t in notes.SelectMany(n => n.TrackNumbers))
            t.CustomerBase = bases.GetValueOrDefault(t.POItemId);
    }

    // ── Get by ID ──────────────────────────────────────────────────────────

    public async Task<ShipmentNoteResponse?> GetByIdAsync(long id)
    {
        var note = await BuildDetailQuery().FirstOrDefaultAsync(s => s.Id == id);
        if (note == null) return null;
        var result = MapResponse(note);
        FillCustomerBases([result], await ShippingCustomerBases.LoadAsync(_db, result.TrackNumbers.Select(t => t.POItemId)));
        return result;
    }

    // ── Create ─────────────────────────────────────────────────────────────

    public async Task<ShipmentNoteResponse> CreateAsync(long createdByUserId, CreateShipmentNoteRequest request)
    {
        var type = string.IsNullOrWhiteSpace(request.Type) ? "DDP" : request.Type;
        if (type != "CPT" && type != "DDP")
            throw new InvalidOperationException("Shipment type must be CPT or DDP.");
        var itemIds = request.Items.Select(i => i.TrackNumberItemId).ToList();

        // CPT validation: all selected items must belong to a single customer
        if (type == "CPT" && itemIds.Any() && (await LoadCustomerIdsAsync(itemIds)).Count > 1)
            throw new InvalidOperationException("CPT shipment notes must contain items for a single customer only.");

        // Only Base 1 shipments carry T-ID / SO#, so Base 1 parts cannot share a note with Base 2–5.
        var usesTidSo = true;
        if (itemIds.Any())
        {
            var bases = await LoadItemBasesAsync(itemIds);
            usesTidSo = bases.All(ShippingBaseAccess.UsesTidSo);
            if (!usesTidSo && bases.Any(ShippingBaseAccess.UsesTidSo))
                throw new InvalidOperationException("Base 1 parts cannot share a shipment note with Base 2–5 parts.");
        }

        var snNumber = await NextNumberAsync("SN");

        var note = new ShipmentNote
        {
            SNNumber = snNumber,
            WarehouseId = request.WarehouseId,
            Type = type,
            TId = usesTidSo ? request.TId : null,
            SONumber = usesTidSo ? request.SONumber : null,
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

    // ── PickUp (Base 2–5) ──────────────────────────────────────────────────

    public async Task<ShipmentNoteResponse> CreatePickupAsync(long createdByUserId, CreatePickupRequest request, IReadOnlyList<IFormFile> files, IReadOnlySet<int>? allowedBases)
    {
        var itemIds = request.TrackNumberItemIds.Distinct().ToList();
        if (itemIds.Count == 0) throw new InvalidOperationException("Select at least one part.");
        if (files.Count == 0) throw new InvalidOperationException("Upload the customer's documents.");

        var items = await _db.Set<TrackNumberItem>()
            .Include(i => i.TrackNumber)
            .Where(i => itemIds.Contains(i.Id))
            .ToListAsync();
        if (items.Count != itemIds.Count) throw new KeyNotFoundException("Some parts were not found.");
        if (items.Any(i => i.Status != "Accepted" || i.TrackNumber.WarehouseId != request.WarehouseId))
            throw new InvalidOperationException("Only accepted parts in the selected warehouse can be picked up.");

        var trackIds = items.Select(i => i.TrackNumberId).Distinct().ToList();
        if (await _db.Set<ShipmentNoteTrackNumber>().AnyAsync(s => trackIds.Contains(s.TrackNumberId)))
            throw new InvalidOperationException("Some of these parts are already on a shipment note.");

        var bases = await LoadItemBasesAsync(itemIds);
        if (!bases.All(ShippingBaseAccess.CanPickUp))
            throw new InvalidOperationException("Only Base 2–5 parts can be picked up.");
        if (allowedBases != null && !bases.All(b => allowedBases.Contains(ShippingBaseAccess.Effective(b))))
            throw new InvalidOperationException("You do not have access to some of these parts' bases.");
        if ((await LoadCustomerIdsAsync(itemIds)).Count > 1)
            throw new InvalidOperationException("A pickup is for one customer — the selected parts belong to several customers.");

        var note = new ShipmentNote
        {
            SNNumber = await NextNumberAsync("PU"),
            WarehouseId = request.WarehouseId,
            Type = "PickUp",
            Status = "Confirmed",
            PickupNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = createdByUserId,
        };
        foreach (var trackId in trackIds)
            note.TrackNumbers.Add(new ShipmentNoteTrackNumber { TrackNumberId = trackId });

        // The parts left with the customer — the end of the shipping road.
        foreach (var track in items.Select(i => i.TrackNumber).DistinctBy(t => t.Id))
            track.Status = "Delivered to Customer";

        // Files are written to a staging folder first so the note, its links and its documents
        // are saved in one SaveChanges; they move under the note's own folder once it has an id.
        var staging = Path.Combine(PdfRoot, "_staging", Guid.NewGuid().ToString("N"));
        try
        {
            foreach (var doc in await SaveDocumentsAsync(staging, createdByUserId, files))
                note.Documents.Add(doc);

            _db.Set<ShipmentNote>().Add(note);
            await _db.SaveChangesAsync();

            var folder = Path.Combine(PdfRoot, note.Id.ToString());
            Directory.CreateDirectory(folder);
            foreach (var doc in note.Documents)
                File.Move(Path.Combine(staging, doc.FileName), Path.Combine(folder, doc.FileName));
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }

        return (await GetByIdAsync(note.Id))!;
    }

    public async Task<ShipmentNoteResponse?> AddDocumentsAsync(long id, long userId, IReadOnlyList<IFormFile> files)
    {
        if (files.Count == 0) throw new InvalidOperationException("Choose at least one file.");
        if (!await _db.Set<ShipmentNote>().AnyAsync(s => s.Id == id)) return null;

        foreach (var doc in await SaveDocumentsAsync(Path.Combine(PdfRoot, id.ToString()), userId, files))
        {
            doc.ShipmentNoteId = id;
            _db.Set<ShipmentNoteDocument>().Add(doc);
        }
        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<(Stream stream, string fileName, string mimeType)?> DownloadDocumentAsync(long id, long docId)
    {
        var doc = await _db.Set<ShipmentNoteDocument>().AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == docId && d.ShipmentNoteId == id);
        if (doc == null) return null;

        var path = Path.Combine(PdfRoot, id.ToString(), doc.FileName);
        if (!File.Exists(path)) return null;

        return (File.OpenRead(path), doc.OriginalFileName, doc.MimeType ?? "application/octet-stream");
    }

    private static async Task<List<ShipmentNoteDocument>> SaveDocumentsAsync(string folder, long userId, IReadOnlyList<IFormFile> files)
    {
        Directory.CreateDirectory(folder);
        var docs = new List<ShipmentNoteDocument>();
        foreach (var file in files)
        {
            var storedName = $"doc_{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
            await using (var fs = File.Create(Path.Combine(folder, storedName)))
                await file.CopyToAsync(fs);

            docs.Add(new ShipmentNoteDocument
            {
                FileName = storedName,
                OriginalFileName = Path.GetFileName(file.FileName),
                MimeType = string.IsNullOrWhiteSpace(file.ContentType) ? null : file.ContentType,
                FileSizeBytes = file.Length,
                UploadedAt = DateTime.UtcNow,
                UploadedByUserId = userId,
            });
        }
        return docs;
    }

    // ── Lookups shared by Create / PickUp ──────────────────────────────────

    /// <summary>Auto-generated number: {kind}-{yyyy}-{seq:000}, e.g. SN-2026-001 / PU-2026-001.</summary>
    private async Task<string> NextNumberAsync(string kind)
    {
        var prefix = $"{kind}-{DateTime.UtcNow.Year}-";
        var existingCount = await _db.Set<ShipmentNote>().CountAsync(s => s.SNNumber.StartsWith(prefix));
        return $"{prefix}{(existingCount + 1):000}";
    }

    /// <summary>Distinct customers behind the selected parts (parts with no customer are ignored).</summary>
    private async Task<List<long>> LoadCustomerIdsAsync(List<long> itemIds)
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
        return customerIds.Where(id => id != null).Select(id => id!.Value).Distinct().ToList();
    }

    /// <summary>Customer.Base of each selected part (null = no customer).</summary>
    private async Task<List<int?>> LoadItemBasesAsync(List<long> itemIds)
    {
        var poItemIds = await _db.Set<TrackNumberItem>().AsNoTracking()
            .Where(i => itemIds.Contains(i.Id))
            .Select(i => i.POItemId)
            .ToListAsync();
        var bases = await ShippingCustomerBases.LoadAsync(_db, poItemIds);
        return poItemIds.Select(id => bases.GetValueOrDefault(id)).ToList();
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
            .AsSplitQuery()
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
            .Include(s => s.Boxes)
            .Include(s => s.Documents)
                .ThenInclude(d => d.UploadedBy);

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
        PickupNote = s.PickupNote,
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
        Documents = s.Documents?.OrderBy(d => d.UploadedAt).Select(d => new ShipmentNoteDocumentResponse
        {
            Id = d.Id,
            ShipmentNoteId = d.ShipmentNoteId,
            OriginalFileName = d.OriginalFileName,
            MimeType = d.MimeType,
            FileSizeBytes = d.FileSizeBytes,
            UploadedAt = d.UploadedAt,
            UploadedByName = d.UploadedBy?.Name,
        }).ToList() ?? new(),
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
