using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Procument.Module.Catalog.Entities;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Entities;
using Procument.Module.RFQ.Entities;
using Procument.Module.Identity.Services;
using Procument.Module.Identity.Entities; // for permissions
using Procument.Shared.Services;
using System.Security.Cryptography;

namespace Procument.Module.Purchasing.Services;

public interface ISupplierQuoteService
{
    Task<List<SupplierQuoteResponse>> GetByRFQIdAsync(long rfqId, long userId, bool isAdmin);
    Task<SupplierQuoteResponse> SaveAsync(SaveSupplierQuoteRequest request, long userId);
    Task<List<SupplierQuoteResponse>> BulkSaveAsync(long rfqId, BulkSaveQuotesRequest request, long userId);
    Task<bool> DeleteAsync(long id, long userId);
    Task<bool> UpdateOrderAsync(long rfqId, List<SupplierQuoteOrderEntry> items, long userId, bool isAdmin);
    Task<List<SupplierQuoteCertificateResponse>> GetCertificatesForRfqAsync(long rfqId, long userId, bool isAdmin);
    Task<List<SupplierQuoteCertificateResponse>> GetCertificatesAsync(long rfqId, long quoteId, long userId, bool isAdmin);
    Task<List<SupplierQuoteCertificateResponse>> UploadCertificatesAsync(long rfqId, long quoteId, long userId, IEnumerable<IFormFile> files);
    Task<(Stream stream, string fileName, string mimeType)?> DownloadCertificateAsync(long rfqId, long quoteId, long certificateId, long userId, bool isAdmin);
}

public class SupplierQuoteService : ISupplierQuoteService
{
    private const string CertificateRoot = "Documents/SupplierQuoteCertificates";
    private readonly DbContext _db;
    private readonly IPermissionService _permissionService;

    public SupplierQuoteService(DbContext db, IPermissionService permissionService)
    {
        _db = db;
        _permissionService = permissionService;
    }

    /// <summary>Get all supplier quotes for all items in an RFQ.</summary>
    public async Task<List<SupplierQuoteResponse>> GetByRFQIdAsync(long rfqId, long userId, bool isAdmin)
    {
        // 1. Check if user has access to this RFQ
        if (!isAdmin)
        {
            // We need to check if user is Creator OR has Permission.
            // We can fetch RFQ header lightly or check permission table.
            // Ideally re-use RFQService logic, but avoiding circular dep.
            // Let's fetch RFQ UserId.
            var rfq = await _db.Set<RFQHeader>().FirstOrDefaultAsync(r => r.Id == rfqId);
            if (rfq == null) return new List<SupplierQuoteResponse>(); // RFQ doesn't exist

            if (rfq.UserId != userId)
            {
                var hasPermission = await _permissionService.HasPermissionAsync(userId, "RFQ", rfqId.ToString(), "View")
                                 || await _permissionService.HasPermissionAsync(userId, "RFQ", rfqId.ToString(), "Edit");

                if (!hasPermission)
                {
                    // User has no access to this RFQ -> return empty list or throw.
                    // Empty list is safer for list endpoints.
                    return new List<SupplierQuoteResponse>();
                }
            }
        }

        var records = await _db.Set<ProcumentRecord>()
            .Include(r => r.Supplier)
            .Include(r => r.Certificates)
            .Include(r => r.ShopRecords)
                .ThenInclude(s => s.Supplier)
            .Include(r => r.ShopRecords)
                .ThenInclude(s => s.Certificates)
            .Where(r => r.RFQItem.RFQId == rfqId && (r.Type ?? "Procument") != "Shop")
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Id)
            .ToListAsync();

        return records.Select(MapToResponse).ToList();
    }

    /// <summary>Create or update a single supplier quote.</summary>
    public async Task<SupplierQuoteResponse> SaveAsync(SaveSupplierQuoteRequest request, long userId)
    {
        // 1. Check Permissions
        // Since we don't have RFQId easily in Update case, we need to fetch it.
        long rfqId;
        if (request.Id.HasValue && request.Id > 0)
        {
            var existing = await _db.Set<ProcumentRecord>()
                .Include(r => r.RFQItem)
                .FirstOrDefaultAsync(r => r.Id == request.Id.Value);
            if (existing == null) throw new KeyNotFoundException($"Supplier quote {request.Id} not found.");
            rfqId = existing.RFQItem.RFQId;
        }
        else
        {
            // For create, we need RFQItemId to find RFQId
            var rfqItem = await _db.Set<RFQItem>().FindAsync(request.RFQItemId)
                ?? throw new KeyNotFoundException("RFQ Item not found");
            rfqId = rfqItem.RFQId;
        }

        // Check if user is Admin OR has Edit permission
        var hasPermission = await _permissionService.HasPermissionAsync(userId, "RFQ", rfqId.ToString(), "Edit");

        var user = await _db.Set<User>().FindAsync(userId);
        bool isAdmin = user?.Role == "Admin" || user?.Role == "SuperAdmin";

        // Check if user is the owner of THIS RFQ
        var isOwner = await _db.Set<RFQHeader>().AnyAsync(r => r.Id == rfqId && r.UserId == userId);

        if (!isAdmin && !hasPermission && !isOwner)
        {
            throw new UnauthorizedAccessException("User does not have permission to add quotes to this RFQ.");
        }

        // Resolve supplier by name. Normal procurement records require an existing (Catalog)
        // supplier; AR / repair-shop records may still create one on the fly.
        var supplier = await ResolveSupplierAsync(
            request.SupplierName, userId, AllowsSupplierCreation(request.Condition, request.Type));

        ProcumentRecord record;


        if (request.Id.HasValue && request.Id > 0)
        {
            // Update existing
            var existingRecord = await _db.Set<ProcumentRecord>()
                .FirstOrDefaultAsync(r => r.Id == request.Id.Value);
            
            if (existingRecord == null) throw new KeyNotFoundException($"Procurement record {request.Id.Value} not found.");
            record = existingRecord;

            record.SupplierId = supplier.Id;
            record.Qty = request.Qty;
            record.Price = request.Price;
            record.Condition = request.Condition;
            record.Alt = request.Alt;
            record.Unit = request.Unit;
            record.LeadTime = request.LeadTime;
            if (request.Coef_1 != null && request.Coef_2 != null && request.Coef_3 != null)
            {
                record.Coef_1 = request.Coef_1;
                record.Coef_2 = request.Coef_2;
                record.Coef_3 = request.Coef_3;
                record.UnitPrice = request.UnitPrice;
                record.TotalPrice = request.TotalPrice;
            }
            record.ShippingCost = request.ShippingCost;
            record.ShippingPoint = request.ShippingPoint;
            record.CertName = request.CertName;
            
            record.TagDate = request.TagDate;
            record.Note = request.Note;
            record.MyNotes = request.MyNotes;
            record.IsCertificated = request.IsCertificated;
            // Auto-set Type to "Shop" only when Condition is "IN" (repair shop record).
            // AR condition = parent procurement record (stays "Procument").
            record.Type = request.Type ?? (request.Condition == "IN" ? "Shop" : "Procument");
            record.FixPrice = request.FixPrice;
            record.ParentProcumentId = request.ParentProcumentId;
            // Only overwrite when sent, so older clients that do not know the field keep the link.
            if (request.SourceStockItemId.HasValue) record.SourceStockItemId = request.SourceStockItemId;
            record.UserId = userId;
            record.UpdatedAt = DateTime.UtcNow; // editing the cost refreshes its price age
        }
        else
        {
            // Create new
            record = new ProcumentRecord
            {
                RFQItemId = request.RFQItemId,
                SupplierId = supplier.Id,
                Qty = request.Qty,
                Price = request.Price,
                Condition = request.Condition,
                Alt = request.Alt,
                Unit = request.Unit,
                LeadTime = request.LeadTime,
                Coef_1 = request.Coef_1,
                Coef_2 = request.Coef_2,
                Coef_3 = request.Coef_3,
                ShippingCost = request.ShippingCost,
                ShippingPoint = request.ShippingPoint,
                CertName = request.CertName,
                UnitPrice = request.UnitPrice,
                TotalPrice = request.TotalPrice,
                TagDate = request.TagDate,
                Note = request.Note,
                MyNotes = request.MyNotes,
                IsCertificated = request.IsCertificated,
                Type = request.Type ?? (request.Condition == "IN" ? "Shop" : "Procument"),
                FixPrice = request.FixPrice,
                ParentProcumentId = request.ParentProcumentId,
                SourceStockItemId = request.SourceStockItemId,
                UserId = userId
            };
            _db.Set<ProcumentRecord>().Add(record);

            // We can't log ID yet.
        }

        await _db.SaveChangesAsync(); // Get ID

        // ── If parent RFQ is Rejected, reset to In Progress (but keep the rejected quote as history) ──
        {
            var rfqHeader = await _db.Set<RFQHeader>().FindAsync(rfqId);
            if (rfqHeader != null && rfqHeader.Status == "Rejected")
            {
                rfqHeader.Status = "In Progress";
                await _db.SaveChangesAsync();
            }
        }

        // ── Auto-link supplier to part number in junction table ──
        {
            var rfqItemForLink = await _db.Set<RFQItem>()
                .FirstOrDefaultAsync(i => i.Id == record.RFQItemId);

            if (rfqItemForLink != null)
            {
                var alreadyLinked = await _db.Set<PartNumberSupplier>()
                    .AnyAsync(ps => ps.PartNumberId == rfqItemForLink.PartNumberId && ps.SupplierId == supplier.Id);

                if (!alreadyLinked)
                {
                    try
                    {
                        _db.Set<PartNumberSupplier>().Add(new PartNumberSupplier
                        {
                            PartNumberId = rfqItemForLink.PartNumberId,
                            SupplierId = supplier.Id,
                            CreatedAt = DateTime.UtcNow
                        });
                        await _db.SaveChangesAsync();
                    }
                    catch (DbUpdateException ex)
                        when (ex.InnerException?.Message.Contains("duplicate key") == true ||
                              ex.InnerException?.Message.Contains("Cannot insert duplicate") == true)
                    {
                        // Concurrent request already inserted the same link — safe to ignore
                        _db.ChangeTracker.Clear();
                    }
                }
            }
        }

        // ── Auto-add Alt P/N to Alternatives table if not exists ──
        if (!string.IsNullOrWhiteSpace(request.Alt))
        {
            var rfqItem = await _db.Set<RFQItem>()
                .FirstOrDefaultAsync(i => i.Id == request.RFQItemId);

            if (rfqItem != null)
            {
                var altTrimmed = request.Alt.Trim();
                var exists = await _db.Set<Alternative>()
                    .AnyAsync(a => a.PartNumberId == rfqItem.PartNumberId && a.Name == altTrimmed);

                if (!exists)
                {
                    _db.Set<Alternative>().Add(new Alternative
                    {
                        Name = altTrimmed,
                        PartNumberId = rfqItem.PartNumberId,
                        CreatedAt = DateTime.UtcNow
                    });
                    await _db.SaveChangesAsync();
                }
            }
        }

        // Reload with full navigation so response includes shops + their FixPrice
        record = await _db.Set<ProcumentRecord>()
            .Include(r => r.Supplier)
            .Include(r => r.Certificates)
            .Include(r => r.ShopRecords)
                .ThenInclude(s => s.Supplier)
            .Include(r => r.ShopRecords)
                .ThenInclude(s => s.Certificates)
            .FirstAsync(r => r.Id == record.Id);

        return MapToResponse(record);
    }

    /// <summary>Bulk save supplier quotes for an RFQ.</summary>
    public async Task<List<SupplierQuoteResponse>> BulkSaveAsync(long rfqId, BulkSaveQuotesRequest request, long userId)
    {
        // Permission check (once for the whole bulk op)
        // Permission check (once for the whole bulk op)
        var hasPermission = await _permissionService.HasPermissionAsync(userId, "RFQ", rfqId.ToString(), "Edit");
        var user = await _db.Set<User>().FindAsync(userId);
        bool isAdmin = user?.Role == "Admin" || user?.Role == "SuperAdmin";

        var isOwner = await _db.Set<RFQHeader>().AnyAsync(s => s.Id == rfqId && s.UserId == userId);

        if (!isAdmin && !hasPermission && !isOwner)
        {
            throw new UnauthorizedAccessException("User does not have permission to add quotes to this RFQ.");
        }

        // Pre-validate every supplier name up front. SaveAsync (via ResolveSupplierAsync)
        // throws on an unknown name, but it also SaveChanges per quote — so validating here
        // first stops a single bad name from leaving earlier quotes in the batch persisted.
        // AR / repair-shop quotes are exempt (they may create suppliers on the fly).
        var names = request.Quotes
            .Where(q => !string.IsNullOrWhiteSpace(q.SupplierName)
                        && !AllowsSupplierCreation(q.Condition, q.Type))
            .Select(q => SupplierNameNormalizer.Clean(q.SupplierName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (names.Count > 0)
        {
            var existing = (await _db.Set<Supplier>()
                    .AsNoTracking()
                    .Select(s => s.Name)
                    .ToListAsync())
                .Select(SupplierNameNormalizer.Key)
                .ToHashSet(StringComparer.Ordinal);
            var missing = names.Where(n => !existing.Contains(SupplierNameNormalizer.Key(n))).ToList();
            if (missing.Count > 0)
                throw new SupplierNotFoundException(missing);
        }

        var results = new List<SupplierQuoteResponse>();

        // We can reuse SaveAsync but pass userId.
        // OPTIMIZATION: SaveAsync does permission check again. 
        // It's fine for now, or we can refactor internal method.
        // But SaveAsync needs to fetch RFQId for each item to check permission if we don't pass it.
        // Here we know RFQId.

        foreach (var quote in request.Quotes)
        {
            // We can't easily skip check inside SaveAsync without refactoring.
            // Let's just call it. Use cache if performance issue.
            var result = await SaveAsync(quote, userId);
            results.Add(result);
        }

        return results;
    }

    /// <summary>Delete a supplier quote by ID.</summary>
    public async Task<bool> DeleteAsync(long id, long userId)
    {
        var record = await _db.Set<ProcumentRecord>()
            .Include(r => r.RFQItem)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (record == null) return false;

        // Check permission
        long rfqId = record.RFQItem.RFQId;
        var hasPermission = await _permissionService.HasPermissionAsync(userId, "RFQ", rfqId.ToString(), "Edit");
        var user = await _db.Set<User>().FindAsync(userId);
        bool isAdmin = user?.Role == "Admin" || user?.Role == "SuperAdmin";

        if (!isAdmin && !hasPermission)
        {
            throw new UnauthorizedAccessException("User does not have permission to delete quotes from this RFQ.");
        }

        // Delete child shop records first (self-ref FK uses Restrict)
        var shopRecords = await _db.Set<ProcumentRecord>()
            .Where(s => s.ParentProcumentId == record.Id)
            .ToListAsync();
        if (shopRecords.Any())
        {
            _db.Set<ProcumentRecord>().RemoveRange(shopRecords);
        }

        _db.Set<ProcumentRecord>().Remove(record);
        await _db.SaveChangesAsync();

        return true;
    }

    // ──── Helpers ────

    /// <summary>
    /// AR (as-removed) records and repair-shop records may still create suppliers on the fly —
    /// repair shops are often one-off vendors that don't belong in the Catalog. Every other
    /// (normal procurement) record must reference a supplier that already exists.
    /// </summary>
    private static bool AllowsSupplierCreation(string? condition, string? type)
        => condition == "AR" || condition == "IN" || type == "Shop";

    private async Task<Supplier> ResolveSupplierAsync(string supplierName, long userId, bool allowCreate)
    {
        var trimmed = SupplierNameNormalizer.Clean(supplierName);
        var supplierKey = SupplierNameNormalizer.Key(trimmed);

        var user = await _db.Set<User>().FindAsync(userId);
        bool isAdmin = user?.Role == "Admin" || user?.Role == "SuperAdmin";

        // Check if an existing supplier (active or not) exists by name
        // Keep the usual SQL lookup fast, then use normalized comparison as a
        // fallback for legacy names containing invisible or unusual whitespace.
        var lower = trimmed.ToLowerInvariant();
        var existing = await _db.Set<Supplier>()
            .FirstOrDefaultAsync(s => s.Name.ToLower().Trim() == lower);
        if (existing == null)
        {
            var candidates = await _db.Set<Supplier>().ToListAsync();
            existing = candidates.FirstOrDefault(s => SupplierNameNormalizer.Key(s.Name) == supplierKey);
        }

        if (existing != null)
        {
            // If it was soft-deleted (IsActive = false), reactivate it
            if (!existing.IsActive)
            {
                existing.IsActive = true;
                // If it was "Disabled", move it back to Approved (if admin) or Pending
                if (existing.Status == "Disabled")
                {
                    existing.Status = isAdmin ? "Approved" : "Pending";
                }
                await _db.SaveChangesAsync();
            }
            // If it's active but Pending/Rejected/Approved, just return it
            return existing;
        }

        // No match. Normal procurement records must not create suppliers here — they have to be
        // added via the Catalog first (prevents typos/duplicates spawning stray suppliers).
        // AR / repair-shop records are exempt and may still create the supplier on the fly.
        if (!allowCreate)
            throw new SupplierNotFoundException(trimmed);

        var supplier = new Supplier
        {
            Name = trimmed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            Status = isAdmin ? "Approved" : "Pending",
            RequestedByUserId = userId
        };
        _db.Set<Supplier>().Add(supplier);
        await _db.SaveChangesAsync();

        return supplier;
    }

    public async Task<bool> UpdateOrderAsync(long rfqId, List<SupplierQuoteOrderEntry> items, long userId, bool isAdmin)
    {
        if (!isAdmin)
        {
            var rfq = await _db.Set<RFQHeader>().FirstOrDefaultAsync(r => r.Id == rfqId);
            if (rfq == null) return false;
            if (rfq.UserId != userId)
            {
                var hasPermission = await _permissionService.HasPermissionAsync(userId, "RFQ", rfqId.ToString(), "Edit");
                if (!hasPermission) return false;
            }
        }

        var ids = items.Select(i => i.Id).ToList();
        var records = await _db.Set<ProcumentRecord>()
            .Include(r => r.RFQItem)
            .Where(r => ids.Contains(r.Id) && r.RFQItem.RFQId == rfqId)
            .ToListAsync();

        var orderMap = items.ToDictionary(i => i.Id, i => i.SortOrder);
        foreach (var rec in records)
        {
            if (orderMap.TryGetValue(rec.Id, out var so))
                rec.SortOrder = so;
        }
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<SupplierQuoteCertificateResponse>> GetCertificatesForRfqAsync(long rfqId, long userId, bool isAdmin)
    {
        if (!await CanAccessRfqAsync(rfqId, userId, isAdmin, requireEdit: false))
            throw new UnauthorizedAccessException();

        return await _db.Set<SupplierQuoteCertificate>()
            .AsNoTracking()
            .Where(c => c.SupplierQuote.RFQItem.RFQId == rfqId)
            .OrderBy(c => c.SupplierQuote.RFQItemId)
            .ThenBy(c => c.SupplierQuote.Supplier.Name)
            .ThenByDescending(c => c.UploadedAt)
            .Select(c => new SupplierQuoteCertificateResponse
            {
                Id = c.Id,
                SupplierQuoteId = c.SupplierQuoteId,
                PartNumberName = c.SupplierQuote.RFQItem.PartNumber.Name,
                SupplierName = c.SupplierQuote.Supplier.Name,
                Alt = c.SupplierQuote.Alt,
                Reference = c.Reference,
                OriginalFileName = c.OriginalFileName,
                MimeType = c.MimeType,
                FileSizeBytes = c.FileSizeBytes,
                UploadedAt = c.UploadedAt,
                UploadedByName = c.UploadedBy.Name,
            })
            .ToListAsync();
    }

    public async Task<List<SupplierQuoteCertificateResponse>> GetCertificatesAsync(long rfqId, long quoteId, long userId, bool isAdmin)
    {
        if (!await CanAccessRfqAsync(rfqId, userId, isAdmin, requireEdit: false))
            throw new UnauthorizedAccessException();

        var exists = await _db.Set<ProcumentRecord>()
            .AnyAsync(q => q.Id == quoteId && q.RFQItem.RFQId == rfqId);
        if (!exists) throw new KeyNotFoundException("Supplier quote not found.");

        return await _db.Set<SupplierQuoteCertificate>()
            .AsNoTracking()
            .Include(c => c.UploadedBy)
            .Where(c => c.SupplierQuoteId == quoteId)
            .OrderByDescending(c => c.UploadedAt)
            .Select(c => new SupplierQuoteCertificateResponse
            {
                Id = c.Id,
                SupplierQuoteId = c.SupplierQuoteId,
                PartNumberName = c.SupplierQuote.RFQItem.PartNumber.Name,
                SupplierName = c.SupplierQuote.Supplier.Name,
                Alt = c.SupplierQuote.Alt,
                Reference = c.Reference,
                OriginalFileName = c.OriginalFileName,
                MimeType = c.MimeType,
                FileSizeBytes = c.FileSizeBytes,
                UploadedAt = c.UploadedAt,
                UploadedByName = c.UploadedBy.Name,
            })
            .ToListAsync();
    }

    public async Task<List<SupplierQuoteCertificateResponse>> UploadCertificatesAsync(long rfqId, long quoteId, long userId, IEnumerable<IFormFile> files)
    {
        if (!await CanAccessRfqAsync(rfqId, userId, isAdmin: false, requireEdit: true))
            throw new UnauthorizedAccessException();

        var supplierQuote = await _db.Set<ProcumentRecord>()
            .AsNoTracking()
            .Where(q => q.Id == quoteId && q.RFQItem.RFQId == rfqId)
            .Select(q => new
            {
                q.RFQItemId,
                PartNumber = q.RFQItem.PartNumber.Name,
                q.Alt,
            })
            .FirstOrDefaultAsync();
        if (supplierQuote == null) throw new KeyNotFoundException("Supplier quote not found.");

        var validFiles = files.Where(f => f is { Length: > 0 }).ToList();
        if (validFiles.Count == 0) throw new InvalidOperationException("No files were provided.");
        if (validFiles.Any(f => !string.Equals(Path.GetExtension(f.FileName), ".pdf", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Only PDF certificate files can be uploaded.");

        var folder = Path.Combine(CertificateRoot, quoteId.ToString());
        Directory.CreateDirectory(folder);
        var existingCertificateCount = await _db.Set<SupplierQuoteCertificate>()
            .CountAsync(c => c.SupplierQuote.RFQItemId == supplierQuote.RFQItemId);
        var safePartNumber = MakeSafeCertificateName(supplierQuote.PartNumber);
        var altSuffix = string.IsNullOrWhiteSpace(supplierQuote.Alt) ? "" : "-ALT";
        var pendingReferences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<SupplierQuoteCertificateResponse>();
        for (var index = 0; index < validFiles.Count; index++)
        {
            var file = validFiles[index];
            var storedName = $"{Guid.NewGuid():N}.pdf";
            var reference = await GenerateUniqueReferenceAsync(pendingReferences);
            var displayName = $"REF#{reference}-{safePartNumber}{altSuffix}-cert-{existingCertificateCount + index + 1}.pdf";
            await using (var stream = File.Create(Path.Combine(folder, storedName)))
                await file.CopyToAsync(stream);

            var certificate = new SupplierQuoteCertificate
            {
                SupplierQuoteId = quoteId,
                Reference = reference,
                FileName = storedName,
                OriginalFileName = displayName,
                MimeType = "application/pdf",
                FileSizeBytes = file.Length,
                UploadedAt = DateTime.UtcNow,
                UploadedByUserId = userId,
            };
            _db.Set<SupplierQuoteCertificate>().Add(certificate);
            result.Add(MapCertificate(certificate, null));
        }
        await _db.SaveChangesAsync();
        return result;
    }

    private async Task<string> GenerateUniqueReferenceAsync(HashSet<string> pendingReferences)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        while (true)
        {
            var reference = RandomNumberGenerator.GetString(alphabet, 8);
            if (!pendingReferences.Add(reference)) continue;
            if (!await _db.Set<SupplierQuoteCertificate>().AnyAsync(c => c.Reference == reference))
                return reference;
            pendingReferences.Remove(reference);
        }
    }

    private static string MakeSafeCertificateName(string? partNumber)
    {
        var name = string.IsNullOrWhiteSpace(partNumber) ? "part" : partNumber.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
            name = name.Replace(invalid, '-');
        name = name.Trim(' ', '.', '-');
        return string.IsNullOrWhiteSpace(name) ? "part" : name;
    }

    public async Task<(Stream stream, string fileName, string mimeType)?> DownloadCertificateAsync(long rfqId, long quoteId, long certificateId, long userId, bool isAdmin)
    {
        if (!await CanAccessRfqAsync(rfqId, userId, isAdmin, requireEdit: false))
            throw new UnauthorizedAccessException();

        var certificate = await _db.Set<SupplierQuoteCertificate>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == certificateId && c.SupplierQuoteId == quoteId && c.SupplierQuote.RFQItem.RFQId == rfqId);
        if (certificate == null) return null;

        var path = Path.Combine(CertificateRoot, quoteId.ToString(), certificate.FileName);
        return File.Exists(path)
            ? (File.OpenRead(path), certificate.OriginalFileName, certificate.MimeType ?? "application/pdf")
            : null;
    }

    private async Task<bool> CanAccessRfqAsync(long rfqId, long userId, bool isAdmin, bool requireEdit)
    {
        if (isAdmin) return true;
        var user = await _db.Set<User>().FindAsync(userId);
        if (user?.Role is "Admin" or "SuperAdmin") return true;
        var rfq = await _db.Set<RFQHeader>().AsNoTracking().FirstOrDefaultAsync(r => r.Id == rfqId);
        if (rfq == null) return false;
        if (rfq.UserId == userId) return true;
        return await _permissionService.HasPermissionAsync(userId, "RFQ", rfqId.ToString(), requireEdit ? "Edit" : "View")
            || (!requireEdit && await _permissionService.HasPermissionAsync(userId, "RFQ", rfqId.ToString(), "Edit"));
    }

    private static SupplierQuoteResponse MapToResponse(ProcumentRecord r) => new()
    {
        Id = r.Id,
        RFQItemId = r.RFQItemId,
        SupplierId = r.SupplierId,
        SupplierName = r.Supplier.Name,
        SupplierStatus = r.Supplier.Status ?? "Approved",
        SupplierDependency = r.Supplier.Dependency,
        Qty = r.Qty,
        Price = r.Price,
        Condition = r.Condition,
        Alt = r.Alt,
        Unit = r.Unit,
        CertName = r.CertName,
        Coef_1  = r.Coef_1,
        Coef_2 = r.Coef_2,
        Coef_3 = r.Coef_3,
        ShippingPoint = r.ShippingPoint,
        ShippingCost = r.ShippingCost,
        UnitPrice = r.UnitPrice,
        TotalPrice = r.TotalPrice,
        TagDate = r.TagDate,
        LeadTime = r.LeadTime,
        Note = r.Note,
        MyNotes = r.MyNotes,
        IsCertificated = r.IsCertificated,
        Type = r.Type ?? "Procument",
        FixPrice = r.FixPrice,
        SourceStockItemId = r.SourceStockItemId,
        ParentProcumentId = r.ParentProcumentId,
        SortOrder = r.SortOrder,
        CertificateCount = r.Certificates?.Count ?? 0,
        CertReferences = (r.Certificates ?? new List<SupplierQuoteCertificate>())
            .OrderBy(c => c.UploadedAt).Select(c => c.Reference).ToList(),
        ShopRecords = (r.ShopRecords ?? new List<ProcumentRecord>())
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Id)
            .Select(s => MapToResponse(s)).ToList(),
    };

    private static SupplierQuoteCertificateResponse MapCertificate(SupplierQuoteCertificate c, string? uploaderName) => new()
    {
        Id = c.Id,
        SupplierQuoteId = c.SupplierQuoteId,
        Reference = c.Reference,
        OriginalFileName = c.OriginalFileName,
        MimeType = c.MimeType,
        FileSizeBytes = c.FileSizeBytes,
        UploadedAt = c.UploadedAt,
        UploadedByName = uploaderName,
    };
}

/// <summary>
/// Thrown when a supplier quote references a supplier name that does not yet exist.
/// Suppliers can no longer be created straight from the RFQ page — they must be added
/// in the Catalog first. The controller maps this to a 400 with <see cref="MissingSuppliers"/>.
/// </summary>
public class SupplierNotFoundException : Exception
{
    public IReadOnlyList<string> MissingSuppliers { get; }

    public SupplierNotFoundException(string name)
        : this(new[] { name }) { }

    public SupplierNotFoundException(IReadOnlyList<string> names)
        : base(BuildMessage(names))
    {
        MissingSuppliers = names;
    }

    private static string BuildMessage(IReadOnlyList<string> names)
        => names.Count == 1
            ? $"Supplier \"{names[0]}\" was not found. Add it in the Catalog first, then select it here."
            : $"These suppliers were not found: {string.Join(", ", names)}. Add them in the Catalog first, then select them here.";
}
