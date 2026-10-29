using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procument.API.Services;
using Procument.Data;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Entities;
using Procument.Module.Purchasing.Services;
using Procument.Shared.Services;

namespace Procument.API.Controllers;

/// <summary>
/// Office half of the warehouse-app sync (separate project: procument-warehouse).
/// The Shipping page relays encrypted envelopes between this API and the warehouse server:
/// prepare (open tracks → warehouse) → warehouse answers with its changes → import here → ack back.
/// Imported data goes through IShippingService, so it lands exactly as if typed on the track page.
/// </summary>
[Authorize(Roles = "Admin,SuperAdmin,Inventory,Expert")]
[ApiController]
[Route("api/warehouse-sync")]
public class WarehouseSyncController : ControllerBase
{
    /// <summary>Tracks the warehouse still works on. Later stages are closed (read-only) on the phone.</summary>
    private static readonly string[] OpenStatuses = { "Ship to Warehouse", "Received in Warehouse" };

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // One import at a time, and warehouse ids already stored here, so two people pressing Sync
    // together (same batch imported twice before the ack lands) never duplicate files or boxes.
    private static readonly SemaphoreSlim ImportLock = new(1, 1);
    private static readonly ConcurrentDictionary<long, long> ImportedFiles = new();
    private static readonly ConcurrentDictionary<long, long> ImportedBoxes = new();

    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly ICryptoService _crypto;
    private readonly IShippingService _shipping;

    public WarehouseSyncController(AppDbContext db, IConfiguration config, ICryptoService crypto, IShippingService shipping)
    {
        _db = db;
        _config = config;
        _crypto = crypto;
        _shipping = shipping;
    }

    private string SharedKey => _config["WarehouseSync:SharedKey"] is { Length: > 0 } k && k != "REPLACEME"
        ? k
        : throw new InvalidOperationException("WarehouseSync:SharedKey not configured");

    /// <summary>Warehouse server address for the browser relay. The shared key never leaves the server.</summary>
    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        var url = _config["WarehouseSync:Url"]?.TrimEnd('/') ?? string.Empty;
        return Ok(new
        {
            Enabled = url.Length > 0,
            WarehouseUrl = url,
            ApiKey = _config["WarehouseSync:ApiKey"] ?? string.Empty,
        });
    }

    /// <summary>Snapshot of every open track (optionally limited to WarehouseSync:WarehouseIds), encrypted.</summary>
    [HttpGet("prepare")]
    public async Task<IActionResult> Prepare()
    {
        var warehouseIds = _config.GetSection("WarehouseSync:WarehouseIds").Get<long[]>() ?? Array.Empty<long>();

        var query = _db.Set<POItemTrackNumber>().AsNoTracking().Where(t => OpenStatuses.Contains(t.Status));
        if (warehouseIds.Length > 0)
            query = query.Where(t => t.WarehouseId != null && warehouseIds.Contains(t.WarehouseId.Value));

        var tracks = await query
            .Select(t => new
            {
                t.Id, t.TrackNumber, t.Carrier, t.WarehouseId, t.Status, t.POItemId,
                WarehouseName = t.Warehouse != null ? t.Warehouse.Name : null,
                PoNumber = t.POItem.PurchaseOrder != null ? t.POItem.PurchaseOrder.PONumber : null,
                SupplierName = t.POItem.PurchaseOrder != null && t.POItem.PurchaseOrder.Supplier != null
                    ? t.POItem.PurchaseOrder.Supplier.Name : null,
                PartNumber = t.POItem.PartNumber != null ? t.POItem.PartNumber.Name : null,
                PoQty = t.POItem.Qty,
                Item = t.Items.Where(i => i.POItemId == t.POItemId)
                    .Select(i => new { i.ExpectedQty, i.ActualQty, i.Status })
                    .FirstOrDefault(),
                OfficeFileCount = t.Documents.Count(d => d.POItemId == t.POItemId),
            })
            .ToListAsync();

        // Default Expected Qty = PO qty minus what other track numbers already received
        // (same rule as the track page's defaultExpectedQty).
        var poItemIds = tracks.Select(t => t.POItemId).Distinct().ToList();
        var received = await _db.Set<TrackNumberItem>().AsNoTracking()
            .Where(i => poItemIds.Contains(i.POItemId) && i.ActualQty != null)
            .Select(i => new { i.POItemId, Qty = i.ActualQty!.Value, i.TrackNumber.TrackNumber })
            .ToListAsync();

        var trackIds = tracks.Select(t => t.Id).ToList();
        var boxes = await _db.Set<TrackNumberBox>().AsNoTracking()
            .Where(b => trackIds.Contains(b.TrackNumberId))
            .Select(b => new WarehousePushBox
            {
                MainBoxId = b.Id, MainTrackId = b.TrackNumberId, BoxNumber = b.BoxNumber,
                WeightKg = b.WeightKg, HeightCm = b.HeightCm, WidthCm = b.WidthCm, LengthCm = b.LengthCm, Notes = b.Notes,
            })
            .ToListAsync();

        var push = new WarehousePush
        {
            SentAt = DateTime.UtcNow,
            Boxes = boxes,
            Tracks = tracks.Select(t => new WarehousePushTrack
            {
                MainTrackId = t.Id,
                TrackNumber = t.TrackNumber,
                Carrier = t.Carrier,
                WarehouseId = t.WarehouseId,
                WarehouseName = t.WarehouseName,
                PoItemId = t.POItemId,
                PoNumber = t.PoNumber,
                PartNumber = t.PartNumber,
                SupplierName = t.SupplierName,
                ExpectedQty = t.Item?.ExpectedQty ?? Math.Max(0, t.PoQty - received
                    .Where(r => r.POItemId == t.POItemId && r.TrackNumber != t.TrackNumber)
                    .Sum(r => r.Qty)),
                ActualQty = t.Item?.ActualQty,
                Status = t.Status,
                ReviewStatus = t.Item?.Status,
                OfficeFileCount = t.OfficeFileCount,
            }).ToList(),
        };

        var cipher = _crypto.EncryptBrowser(JsonSerializer.Serialize(push, Json), SharedKey, out var iv);
        return Ok(new { CipherText = cipher, Iv = iv, TrackCount = push.Tracks.Count });
    }

    /// <summary>Store one batch of warehouse changes; returns the encrypted ack for the browser to relay back.</summary>
    [HttpPost("import")]
    [RequestSizeLimit(100_000_000)]
    public async Task<IActionResult> Import([FromBody] WarehouseSyncEnvelope envelope)
    {
        WarehouseChanges? changes;
        try { changes = JsonSerializer.Deserialize<WarehouseChanges>(_crypto.DecryptBrowser(envelope.CipherText, envelope.Iv, SharedKey), Json); }
        catch (Exception e) when (e is System.Security.Cryptography.CryptographicException or FormatException or JsonException)
        {
            return BadRequest(new { message = "Could not decrypt the warehouse data — check WarehouseSync:SharedKey on both servers." });
        }
        if (changes == null) return BadRequest(new { message = "Empty warehouse batch." });

        var userId = GetUserId();
        var ack = new WarehouseAck();
        var summary = new WarehouseImportSummary();

        await ImportLock.WaitAsync();
        try
        {
            await ImportQuantitiesAsync(changes, userId, ack, summary);
            await ImportNotesAsync(changes, ack, summary);
            await ImportBoxesAsync(changes, ack, summary);
            await ImportFilesAsync(changes, userId, ack, summary);
        }
        finally
        {
            ImportLock.Release();
        }

        var cipher = _crypto.EncryptBrowser(JsonSerializer.Serialize(ack, Json), SharedKey, out var iv);
        return Ok(new { Ack = new WarehouseSyncEnvelope { CipherText = cipher, Iv = iv }, Summary = summary });
    }

    // ── Import steps ──────────────────────────────────────────────────────
    // Each item is acked only once stored (or once it can never be stored), so a failure is
    // retried on the next sync instead of being lost.

    private async Task ImportQuantitiesAsync(WarehouseChanges changes, long userId, WarehouseAck ack, WarehouseImportSummary summary)
    {
        foreach (var c in changes.Parts)
        {
            var done = new WarehouseVersionAck { Id = c.PartId, Version = c.Version };
            try
            {
                var track = await _db.Set<POItemTrackNumber>().AsNoTracking().FirstOrDefaultAsync(t => t.Id == c.MainTrackId);
                if (track == null) { summary.Skipped.Add($"Track #{c.MainTrackId} no longer exists — qty dropped."); ack.Parts.Add(done); continue; }
                // Submitting moves the PO back to "waiting for shipment approval", so never do it on a track that moved on.
                if (!OpenStatuses.Contains(track.Status)) { summary.Skipped.Add($"{track.TrackNumber}: already '{track.Status}' — qty not changed."); ack.Parts.Add(done); continue; }
                if (c.ActualQty == null) { ack.Parts.Add(done); continue; }

                var existing = await _db.Set<TrackNumberItem>().AsNoTracking()
                    .FirstOrDefaultAsync(i => i.TrackNumberId == track.Id && i.POItemId == c.PoItemId);
                await _shipping.SubmitItemsAsync(track.Id, userId, new SubmitTrackNumberItemsRequest
                {
                    Items =
                    {
                        new TrackNumberItemInput
                        {
                            POItemId = c.PoItemId,
                            ExpectedQty = existing?.ExpectedQty ?? c.ExpectedQty,
                            ActualQty = c.ActualQty,
                            IsAvailable = c.ActualQty > 0,
                        },
                    },
                });
                ack.Parts.Add(done);
                summary.Quantities++;
            }
            catch (Exception e)
            {
                _db.ChangeTracker.Clear();
                summary.Skipped.Add($"Qty for track #{c.MainTrackId} failed ({e.Message}) — will retry next sync.");
            }
        }
    }

    private async Task ImportNotesAsync(WarehouseChanges changes, WarehouseAck ack, WarehouseImportSummary summary)
    {
        foreach (var n in changes.Notes)
        {
            var tracks = await _db.Set<POItemTrackNumber>().Where(t => n.MainTrackIds.Contains(t.Id)).ToListAsync();
            foreach (var t in tracks) t.Notes = MergeWarehouseNote(t.Notes, n.Note, n.By);
            await _db.SaveChangesAsync();
            ack.Notes.Add(new WarehouseVersionAck { Id = n.ShipmentId, Version = n.Version });
            summary.Notes++;
        }
    }

    private async Task ImportBoxesAsync(WarehouseChanges changes, WarehouseAck ack, WarehouseImportSummary summary)
    {
        foreach (var b in changes.Boxes)
        {
            var request = new SaveTrackBoxRequest
            {
                BoxNumber = b.BoxNumber, WeightKg = b.WeightKg, HeightCm = b.HeightCm,
                WidthCm = b.WidthCm, LengthCm = b.LengthCm, Notes = b.Notes,
            };

            var mainId = b.MainBoxId ?? (ImportedBoxes.TryGetValue(b.BoxId, out var known) ? known : (long?)null);
            var existing = mainId == null ? null
                : await _db.Set<TrackNumberBox>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == mainId);

            if (existing != null)
            {
                await _shipping.UpdateTrackBoxAsync(existing.TrackNumberId, existing.Id, request);
                ack.Boxes.Add(new WarehouseBoxAck { BoxId = b.BoxId, Version = b.Version, MainBoxId = existing.Id });
            }
            else
            {
                var trackId = await PrimaryTrackIdAsync(b.MainTrackIds);
                if (trackId == null) { summary.Skipped.Add($"Box {b.BoxNumber}: its shipment no longer exists in the office."); continue; }
                var created = await _shipping.AddTrackBoxAsync(trackId.Value, request);
                ImportedBoxes[b.BoxId] = created.Id;
                ack.Boxes.Add(new WarehouseBoxAck { BoxId = b.BoxId, Version = b.Version, MainBoxId = created.Id });
            }
            summary.Boxes++;
        }

        foreach (var d in changes.DeletedBoxes)
        {
            var box = await _db.Set<TrackNumberBox>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == d.MainBoxId);
            if (box != null)
            {
                await _shipping.DeleteTrackBoxAsync(box.TrackNumberId, box.Id);
                summary.DeletedBoxes++;
            }
            ack.DeletedBoxIds.Add(d.BoxId);
        }
    }

    private async Task ImportFilesAsync(WarehouseChanges changes, long userId, WarehouseAck ack, WarehouseImportSummary summary)
    {
        foreach (var f in changes.Files)
        {
            if (ImportedFiles.TryGetValue(f.FileId, out var already))
            {
                ack.Files.Add(new WarehouseFileAck { FileId = f.FileId, MainDocId = already });
                continue;
            }
            try
            {
                var trackId = await PrimaryTrackIdAsync(f.MainTrackIds);
                if (trackId == null) { summary.Skipped.Add($"{f.FileName}: its shipment no longer exists in the office."); continue; }

                var bytes = Convert.FromBase64String(f.DataBase64);
                using var stream = new MemoryStream(bytes);
                var formFile = new FormFile(stream, 0, bytes.Length, "file", f.FileName)
                {
                    Headers = new HeaderDictionary(),
                    ContentType = f.MimeType,
                };
                var doc = f.PoItemId.HasValue
                    ? await _shipping.UploadPartDocAsync(trackId.Value, f.PoItemId.Value, userId, formFile)
                    : await _shipping.UploadTrackDocAsync(trackId.Value, userId, formFile);

                ImportedFiles[f.FileId] = doc.Id;
                ack.Files.Add(new WarehouseFileAck { FileId = f.FileId, MainDocId = doc.Id });
                summary.Files++;
            }
            catch (Exception e)
            {
                _db.ChangeTracker.Clear();
                summary.Skipped.Add($"{f.FileName} failed ({e.Message}) — will retry next sync.");
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Shipment-level documents and boxes go on the newest track of the group — the one the
    /// Shipping list opens (it lists newest first and shows shipment documents from that track).
    /// </summary>
    private async Task<long?> PrimaryTrackIdAsync(List<long> trackIds)
    {
        if (trackIds.Count == 0) return null;
        return await _db.Set<POItemTrackNumber>().AsNoTracking()
            .Where(t => trackIds.Contains(t.Id))
            .OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
            .Select(t => (long?)t.Id)
            .FirstOrDefaultAsync();
    }

    private const string NoteMarker = "[Warehouse";
    private const int NotesMaxLength = 1000; // POItemTrackNumber.Notes column

    /// <summary>Replaces the "[Warehouse · name] …" line in the track notes, keeping the office's own lines.</summary>
    private static string? MergeWarehouseNote(string? existing, string? note, string? by)
    {
        var office = string.Join("\n", (existing ?? string.Empty)
            .Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Where(l => l.Length > 0 && !l.StartsWith(NoteMarker, StringComparison.Ordinal)));
        if (string.IsNullOrWhiteSpace(note)) return office.Length == 0 ? null : office;

        var oneLine = string.Join(" / ", note.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        var line = $"{NoteMarker}{(string.IsNullOrWhiteSpace(by) ? "" : $" · {by}")}] {oneLine}";
        var room = NotesMaxLength - (office.Length == 0 ? 0 : office.Length + 1);
        if (room <= 0) return office[..Math.Min(office.Length, NotesMaxLength)];
        if (line.Length > room) line = line[..Math.Max(0, room - 1)] + "…";
        return office.Length == 0 ? line : $"{office}\n{line}";
    }

    private long GetUserId()
    {
        var str = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!long.TryParse(str, out var id))
            throw new UnauthorizedAccessException("Invalid user token.");
        return id;
    }
}
