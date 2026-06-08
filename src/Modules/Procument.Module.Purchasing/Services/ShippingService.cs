using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Entities;
using Procument.Module.RFQ.Entities;
using Procument.Shared.Services;

namespace Procument.Module.Purchasing.Services;

public interface IShippingService
{
    // Track Numbers for Inventory user
    Task<List<ShippingTrackResponse>> GetTrackNumbersForUserAsync(long userId, bool isAdminOrSyd = false);

    // Submit / update per-part items
    Task<List<TrackNumberItemResponse>> SubmitItemsAsync(long trackId, long submittedByUserId, SubmitTrackNumberItemsRequest request);
    Task<TrackNumberItemResponse?> UpdateItemAsync(long trackId, long itemId, UpdateTrackNumberItemRequest request);

    // Reject a track number → PO status Issue
    Task<bool> RejectTrackAsync(long trackId, long userId);

    // Documents
    Task<TrackNumberDocumentResponse> UploadTrackDocAsync(long trackId, long userId, IFormFile file);
    Task<TrackNumberDocumentResponse> UploadPartDocAsync(long trackId, long poItemId, long userId, IFormFile file);
    Task<List<TrackNumberDocumentResponse>> GetDocumentsAsync(long trackId);
    Task<(Stream stream, string fileName, string mimeType)?> DownloadDocAsync(long docId);
    Task<bool> DeleteDocAsync(long docId, long userId);

    // Review
    Task<TrackNumberItemResponse?> ReviewItemAsync(long trackId, long itemId, long reviewerId, ReviewTrackNumberItemRequest request);

    // Ready for SN
    Task<List<ReadyForSnItemResponse>> GetReadyForSnAsync(long? warehouseId = null);

    // Single track review (Admin/Expert)
    Task<ShippingTrackResponse?> GetTrackForReviewAsync(long trackId);

    // Track Number Boxes
    Task<TrackBoxResponse> AddTrackBoxAsync(long trackId, SaveTrackBoxRequest request);
    Task<TrackBoxResponse?> UpdateTrackBoxAsync(long trackId, long boxId, SaveTrackBoxRequest request);
    Task<bool> DeleteTrackBoxAsync(long trackId, long boxId);
    Task<List<TrackBoxResponse>> GetTrackBoxesAsync(long trackId);
}

public class ShippingService : IShippingService
{
    private readonly DbContext _db;
    private readonly IWarehouseService _warehouseService;
    private readonly IPurchaseOrderService _poService;
    private readonly INotificationService _notifications;
    private const string DocsRoot = "Documents/TrackNumbers";

    public ShippingService(DbContext db, IWarehouseService warehouseService, IPurchaseOrderService poService, INotificationService notifications)
    {
        _db = db;
        _warehouseService = warehouseService;
        _poService = poService;
        _notifications = notifications;
    }

    // ── Track Numbers for user ─────────────────────────────────────────────

    public async Task<List<ShippingTrackResponse>> GetTrackNumbersForUserAsync(long userId, bool isAdminOrSyd = false)
    {
        IQueryable<POItemTrackNumber> query = _db.Set<POItemTrackNumber>()
            .AsNoTracking()
            .Include(t => t.Warehouse)
            .Include(t => t.POItem).ThenInclude(i => i.PurchaseOrder)
            .Include(t => t.POItem).ThenInclude(i => i.PartNumber)
            .Include(t => t.Items).ThenInclude(i => i.ReviewedBy)
            .Include(t => t.Documents).ThenInclude(d => d.UploadedBy)
            .Include(t => t.Boxes);

        if (!isAdminOrSyd)
        {
            var warehouseIds = await _warehouseService.GetWarehouseIdsForUserAsync(userId);
            query = query.Where(t => t.WarehouseId.HasValue && warehouseIds.Contains(t.WarehouseId.Value));
        }

        var tracks = await query.OrderByDescending(t => t.CreatedAt).ToListAsync();

        return tracks.Select(t => MapTrackResponse(t)).ToList();
    }

    // ── Track Boxes ───────────────────────────────────────────────────────

    public async Task<TrackBoxResponse> AddTrackBoxAsync(long trackId, SaveTrackBoxRequest request)
    {
        var box = new TrackNumberBox
        {
            TrackNumberId = trackId,
            BoxNumber = request.BoxNumber,
            WeightKg = request.WeightKg,
            HeightCm = request.HeightCm,
            WidthCm = request.WidthCm,
            LengthCm = request.LengthCm,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow,
        };
        _db.Set<TrackNumberBox>().Add(box);
        await _db.SaveChangesAsync();
        return MapBoxResponse(box);
    }

    public async Task<TrackBoxResponse?> UpdateTrackBoxAsync(long trackId, long boxId, SaveTrackBoxRequest request)
    {
        var box = await _db.Set<TrackNumberBox>().FirstOrDefaultAsync(b => b.Id == boxId && b.TrackNumberId == trackId);
        if (box == null) return null;
        box.BoxNumber = request.BoxNumber;
        box.WeightKg = request.WeightKg;
        box.HeightCm = request.HeightCm;
        box.WidthCm = request.WidthCm;
        box.LengthCm = request.LengthCm;
        box.Notes = request.Notes;
        await _db.SaveChangesAsync();
        return MapBoxResponse(box);
    }

    public async Task<bool> DeleteTrackBoxAsync(long trackId, long boxId)
    {
        var box = await _db.Set<TrackNumberBox>().FirstOrDefaultAsync(b => b.Id == boxId && b.TrackNumberId == trackId);
        if (box == null) return false;
        _db.Set<TrackNumberBox>().Remove(box);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<TrackBoxResponse>> GetTrackBoxesAsync(long trackId)
    {
        var boxes = await _db.Set<TrackNumberBox>()
            .AsNoTracking()
            .Where(b => b.TrackNumberId == trackId)
            .OrderBy(b => b.BoxNumber)
            .ToListAsync();
        return boxes.Select(MapBoxResponse).ToList();
    }

    // ── Submit items ───────────────────────────────────────────────────────

    public async Task<List<TrackNumberItemResponse>> SubmitItemsAsync(long trackId, long submittedByUserId, SubmitTrackNumberItemsRequest request)
    {
        var track = await _db.Set<POItemTrackNumber>()
            .Include(t => t.Items)
            .FirstOrDefaultAsync(t => t.Id == trackId);
        if (track == null) throw new KeyNotFoundException("Track number not found.");

        foreach (var input in request.Items)
        {
            var existing = track.Items.FirstOrDefault(i => i.POItemId == input.POItemId);
            if (existing != null)
            {
                existing.ExpectedQty = input.ExpectedQty;
                if (input.ActualQty.HasValue) existing.ActualQty = input.ActualQty;
                if (input.IsAvailable.HasValue) existing.IsAvailable = input.IsAvailable;
            }
            else
            {
                _db.Set<TrackNumberItem>().Add(new TrackNumberItem
                {
                    TrackNumberId = trackId,
                    POItemId = input.POItemId,
                    ExpectedQty = input.ExpectedQty,
                    ActualQty = input.ActualQty,
                    IsAvailable = input.IsAvailable,
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow,
                });
            }
        }

        // Auto-advance track status to "Received in Warehouse" when items are first submitted
        if (track.Status == "Ship to Warehouse")
            track.Status = "Received in Warehouse";

        await _db.SaveChangesAsync();

        // Notify admins that Inventory submitted parts
        var submitter = await _db.Set<Module.Identity.Entities.User>().FindAsync(submittedByUserId);
        await _notifications.CreateForAllAdminsAsync(
            "TrackSubmitted", "TrackNumber", trackId, track.TrackNumber,
            $"{submitter?.Name ?? "Inventory"} submitted parts for Track {track.TrackNumber}",
            submittedByUserId, submitter?.Name);

        // Re-load to return populated response
        var updated = await _db.Set<TrackNumberItem>()
            .AsNoTracking()
            .Include(i => i.POItem).ThenInclude(p => p.PartNumber)
            .Include(i => i.ReviewedBy)
            .Where(i => i.TrackNumberId == trackId)
            .ToListAsync();

        return updated.Select(MapItemResponse).ToList();
    }

    public async Task<TrackNumberItemResponse?> UpdateItemAsync(long trackId, long itemId, UpdateTrackNumberItemRequest request)
    {
        var item = await _db.Set<TrackNumberItem>()
            .Include(i => i.POItem).ThenInclude(p => p.PartNumber)
            .FirstOrDefaultAsync(i => i.Id == itemId && i.TrackNumberId == trackId);
        if (item == null) return null;

        if (request.ExpectedQty.HasValue) item.ExpectedQty = request.ExpectedQty.Value;
        if (request.ActualQty.HasValue) item.ActualQty = request.ActualQty;
        if (request.IsAvailable.HasValue) item.IsAvailable = request.IsAvailable;

        await _db.SaveChangesAsync();
        return MapItemResponse(item);
    }

    // ── Reject track ───────────────────────────────────────────────────────

    public async Task<bool> RejectTrackAsync(long trackId, long userId)
    {
        var track = await _db.Set<POItemTrackNumber>()
            .Include(t => t.POItem)
            .FirstOrDefaultAsync(t => t.Id == trackId);
        if (track == null) return false;

        track.Status = "Rejected";

        // Set PO status to Issue if it has a PO
        if (track.POItem?.POId.HasValue == true)
        {
            await _poService.UpdateStatusAsync(track.POItem.POId!.Value, "Issue", isAdmin: true, isSuperAdmin: true);
        }

        await _db.SaveChangesAsync();

        var rejector = await _db.Set<Module.Identity.Entities.User>().FindAsync(userId);
        await _notifications.CreateForAllAdminsAsync(
            "TrackRejected", "TrackNumber", trackId, track.TrackNumber,
            $"Track {track.TrackNumber} was rejected by {rejector?.Name ?? "Inventory"}",
            userId, rejector?.Name);

        return true;
    }

    // ── Documents ─────────────────────────────────────────────────────────

    public async Task<TrackNumberDocumentResponse> UploadTrackDocAsync(long trackId, long userId, IFormFile file)
        => await SaveDoc(trackId, null, userId, file);

    public async Task<TrackNumberDocumentResponse> UploadPartDocAsync(long trackId, long poItemId, long userId, IFormFile file)
        => await SaveDoc(trackId, poItemId, userId, file);

    private async Task<TrackNumberDocumentResponse> SaveDoc(long trackId, long? poItemId, long userId, IFormFile file)
    {
        var folder = poItemId.HasValue
            ? Path.Combine(DocsRoot, trackId.ToString(), "parts", poItemId.Value.ToString())
            : Path.Combine(DocsRoot, trackId.ToString(), "docs");

        Directory.CreateDirectory(folder);

        var ext = Path.GetExtension(file.FileName);
        var storedName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(folder, storedName);

        await using var fs = File.Create(fullPath);
        await file.CopyToAsync(fs);

        var doc = new TrackNumberDocument
        {
            TrackNumberId = trackId,
            POItemId = poItemId,
            FileName = storedName,
            OriginalFileName = file.FileName,
            MimeType = file.ContentType,
            FileSizeBytes = file.Length,
            UploadedAt = DateTime.UtcNow,
            UploadedByUserId = userId,
        };
        _db.Set<TrackNumberDocument>().Add(doc);
        await _db.SaveChangesAsync();

        return MapDocResponse(doc, null);
    }

    public async Task<List<TrackNumberDocumentResponse>> GetDocumentsAsync(long trackId)
    {
        var docs = await _db.Set<TrackNumberDocument>()
            .AsNoTracking()
            .Include(d => d.UploadedBy)
            .Where(d => d.TrackNumberId == trackId)
            .OrderBy(d => d.UploadedAt)
            .ToListAsync();
        return docs.Select(d => MapDocResponse(d, d.UploadedBy?.Name)).ToList();
    }

    public async Task<(Stream stream, string fileName, string mimeType)?> DownloadDocAsync(long docId)
    {
        var doc = await _db.Set<TrackNumberDocument>().AsNoTracking().FirstOrDefaultAsync(d => d.Id == docId);
        if (doc == null) return null;

        var folder = doc.POItemId.HasValue
            ? Path.Combine(DocsRoot, doc.TrackNumberId.ToString(), "parts", doc.POItemId.Value.ToString())
            : Path.Combine(DocsRoot, doc.TrackNumberId.ToString(), "docs");

        var path = Path.Combine(folder, doc.FileName);
        if (!File.Exists(path)) return null;

        return (File.OpenRead(path), doc.OriginalFileName, doc.MimeType ?? "application/octet-stream");
    }

    public async Task<bool> DeleteDocAsync(long docId, long userId)
    {
        var doc = await _db.Set<TrackNumberDocument>().FindAsync(docId);
        if (doc == null) return false;

        var folder = doc.POItemId.HasValue
            ? Path.Combine(DocsRoot, doc.TrackNumberId.ToString(), "parts", doc.POItemId.Value.ToString())
            : Path.Combine(DocsRoot, doc.TrackNumberId.ToString(), "docs");

        var path = Path.Combine(folder, doc.FileName);
        if (File.Exists(path)) File.Delete(path);

        _db.Set<TrackNumberDocument>().Remove(doc);
        await _db.SaveChangesAsync();
        return true;
    }

    // ── Review ────────────────────────────────────────────────────────────

    public async Task<TrackNumberItemResponse?> ReviewItemAsync(long trackId, long itemId, long reviewerId, ReviewTrackNumberItemRequest request)
    {
        var item = await _db.Set<TrackNumberItem>()
            .Include(i => i.POItem).ThenInclude(p => p.PartNumber)
            .Include(i => i.ReviewedBy)
            .FirstOrDefaultAsync(i => i.Id == itemId && i.TrackNumberId == trackId);
        if (item == null) return null;

        item.Status = request.Action == "Accept" ? "Accepted" : "Rejected";
        item.ReviewedByUserId = reviewerId;
        item.ReviewedAt = DateTime.UtcNow;
        item.ReviewNote = request.Note;

        await _db.SaveChangesAsync();

        // Notify Inventory users assigned to this track's warehouse
        var track = await _db.Set<POItemTrackNumber>().FindAsync(trackId);
        if (track?.WarehouseId.HasValue == true)
        {
            var inventoryUserIds = await _db.Set<UserWarehouse>()
                .Where(uw => uw.WarehouseId == track.WarehouseId.Value)
                .Select(uw => uw.UserId)
                .ToListAsync();

            if (inventoryUserIds.Count > 0)
            {
                var reviewer = await _db.Set<Module.Identity.Entities.User>().FindAsync(reviewerId);
                var partName = item.POItem?.PartNumber?.Name ?? "part";
                var notifType = request.Action == "Accept" ? "PartAccepted" : "PartRejected";
                var noteText = string.IsNullOrEmpty(request.Note) ? string.Empty : $": {request.Note}";
                await _notifications.CreateForUsersAsync(
                    inventoryUserIds, notifType, "TrackNumber", trackId, track.TrackNumber,
                    $"Part {partName} was {item.Status.ToLower()} by {reviewer?.Name ?? "admin"}{noteText}",
                    reviewerId, reviewer?.Name);
            }
        }

        return MapItemResponse(item);
    }

    // ── Ready for SN ──────────────────────────────────────────────────────

    public async Task<List<ReadyForSnItemResponse>> GetReadyForSnAsync(long? warehouseId = null)
    {
        var assignedTrackIds = await _db.Set<ShipmentNoteTrackNumber>()
            .Select(s => s.TrackNumberId)
            .Distinct()
            .ToListAsync();

        var baseQuery =
            from item in _db.Set<TrackNumberItem>().AsNoTracking()
            join track in _db.Set<POItemTrackNumber>().AsNoTracking() on item.TrackNumberId equals track.Id
            join warehouse in _db.Set<Warehouse>().AsNoTracking() on track.WarehouseId equals (long?)warehouse.Id into wh
            from warehouse in wh.DefaultIfEmpty()
            join poItem in _db.Set<POItem>().AsNoTracking() on item.POItemId equals poItem.Id
            join partNumber in _db.Set<Procument.Module.Catalog.Entities.PartNumber>().AsNoTracking() on poItem.PartNumberId equals (long?)partNumber.Id into pn
            from partNumber in pn.DefaultIfEmpty()
            join supplier in _db.Set<Procument.Module.Catalog.Entities.Supplier>().AsNoTracking() on (long?)partNumber.SupplierId equals (long?)supplier.Id into sup
            from supplier in sup.DefaultIfEmpty()
            join procItem in _db.Set<ProcurementItem>().AsNoTracking() on (long?)poItem.SourceProcurementItemId equals (long?)procItem.Id into pi
            from procItem in pi.DefaultIfEmpty()
            join rfq in _db.Set<RFQHeader>().AsNoTracking() on (long?)procItem.SourceRfqId equals (long?)rfq.Id into rfqs
            from rfq in rfqs.DefaultIfEmpty()
            join customer in _db.Set<Procument.Module.Catalog.Entities.Customer>().AsNoTracking() on (long?)rfq.CustomerId equals (long?)customer.Id into cu
            from customer in cu.DefaultIfEmpty()
            where item.Status == "Accepted" && item.ActualQty.HasValue
                  && !assignedTrackIds.Contains(item.TrackNumberId)
            select new ReadyForSnItemResponse
            {
                TrackNumberItemId = item.Id,
                TrackNumberId = item.TrackNumberId,
                TrackNumber = track.TrackNumber,
                POItemId = item.POItemId,
                PartNumberName = partNumber != null ? partNumber.Name : null,
                PartDescription = partNumber != null ? partNumber.Description : null,
                SupplierName = supplier != null ? supplier.Name : null,
                ActualQty = item.ActualQty!.Value,
                WarehouseId = track.WarehouseId,
                WarehouseName = warehouse != null ? warehouse.Name : null,
                CustomerId = rfq != null ? (long?)rfq.CustomerId : null,
                CustomerName = customer != null ? customer.Name : null,
                CustomerCode = customer != null ? customer.CustomerCode : null,
            };

        if (warehouseId.HasValue)
            baseQuery = baseQuery.Where(i => i.WarehouseId == warehouseId.Value);

        return await baseQuery.ToListAsync();
    }

    // ── Single track review (Admin/Expert) ────────────────────────────────

    public async Task<ShippingTrackResponse?> GetTrackForReviewAsync(long trackId)
    {
        var track = await _db.Set<POItemTrackNumber>()
            .AsNoTracking()
            .Include(t => t.Warehouse)
            .Include(t => t.POItem).ThenInclude(i => i.PurchaseOrder)
            .Include(t => t.POItem).ThenInclude(i => i.PartNumber)
            .Include(t => t.Items).ThenInclude(i => i.ReviewedBy)
            .Include(t => t.Documents).ThenInclude(d => d.UploadedBy)
            .Include(t => t.Boxes)
            .FirstOrDefaultAsync(t => t.Id == trackId);

        return track == null ? null : MapTrackResponse(track);
    }

    // ── Mapping helpers ───────────────────────────────────────────────────

    private static ShippingTrackResponse MapTrackResponse(POItemTrackNumber t) => new()
    {
        Id = t.Id,
        TrackNumber = t.TrackNumber,
        Carrier = t.Carrier,
        Notes = t.Notes,
        Status = t.Status,
        WarehouseId = t.WarehouseId,
        WarehouseName = t.Warehouse?.Name,
        POItemId = t.POItemId,
        POId = t.POItem?.POId ?? 0,
        PONumber = t.POItem?.PurchaseOrder?.PONumber,
        PartNumberName = t.POItem?.PartNumber?.Name,
        PoItemQty = t.POItem?.Qty ?? 0,
        CreatedAt = t.CreatedAt,
        Items = t.Items?.Select(MapItemResponse).ToList() ?? new(),
        Documents = t.Documents?.Select(d => MapDocResponse(d, d.UploadedBy?.Name)).ToList() ?? new(),
        Boxes = t.Boxes?.Select(MapBoxResponse).ToList() ?? new(),
    };

    private static TrackBoxResponse MapBoxResponse(TrackNumberBox b) => new()
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
    };

    private static TrackNumberItemResponse MapItemResponse(TrackNumberItem i) => new()
    {
        Id = i.Id,
        TrackNumberId = i.TrackNumberId,
        POItemId = i.POItemId,
        PartNumberName = i.POItem?.PartNumber?.Name,
        ExpectedQty = i.ExpectedQty,
        ActualQty = i.ActualQty,
        IsAvailable = i.IsAvailable,
        Status = i.Status,
        ReviewNote = i.ReviewNote,
        ReviewedByUserId = i.ReviewedByUserId,
        ReviewedByName = i.ReviewedBy?.Name,
        ReviewedAt = i.ReviewedAt,
        CreatedAt = i.CreatedAt,
    };

    private static TrackNumberDocumentResponse MapDocResponse(TrackNumberDocument d, string? uploaderName) => new()
    {
        Id = d.Id,
        TrackNumberId = d.TrackNumberId,
        POItemId = d.POItemId,
        FileName = d.FileName,
        OriginalFileName = d.OriginalFileName,
        MimeType = d.MimeType,
        FileSizeBytes = d.FileSizeBytes,
        UploadedAt = d.UploadedAt,
        UploadedByUserId = d.UploadedByUserId,
        UploadedByName = uploaderName,
    };
}
