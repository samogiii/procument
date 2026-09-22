using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Purchasing.Entities;
using Procument.Shared.Audit;
using Procument.Shared.Services;

namespace Procument.Module.OurInventory.Controllers;

public sealed class PurchaseOrderDocumentResponse
{
    public long Id { get; set; }
    public long POId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? DocumentNumber { get; set; }
    public decimal? Amount { get; set; }
    public long UploadedBy { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

[ApiController]
[Route("api/purchase-orders/{poId:long}/documents")]
[Authorize(Roles = "Admin,SuperAdmin,Expert,Payment,AHM,Inventory")]
public sealed class PurchaseOrderDocumentsController(DbContext db, IDocumentStorageService storage) : ControllerBase
{
    private static readonly IReadOnlyDictionary<string, string> CategoryFolders =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PurchaseOrderDocumentCategories.SupplierPI] = "Supplier PI",
            [PurchaseOrderDocumentCategories.SupplierInvoice] = "Supplier Invoice",
            [PurchaseOrderDocumentCategories.Certificate] = "Certificate",
            [PurchaseOrderDocumentCategories.PackingList] = "Packing List",
            [PurchaseOrderDocumentCategories.Other] = "Other",
        };

    [HttpGet]
    public async Task<ActionResult<List<PurchaseOrderDocumentResponse>>> List(long poId)
    {
        if (!await IsStockPoAsync(poId)) return NotFound();
        return Ok(await db.Set<PurchaseOrderDocument>().AsNoTracking()
            .Where(d => d.POId == poId)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new PurchaseOrderDocumentResponse
            {
                Id = d.Id,
                POId = d.POId,
                Category = d.Category,
                FileName = d.FileName,
                DocumentNumber = d.DocumentNumber,
                Amount = d.Amount,
                UploadedBy = d.UploadedBy,
                UploadedByName = d.UploadedByUser.Name,
                CreatedAt = d.CreatedAt,
            }).ToListAsync());
    }

    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin,Expert,Inventory")]
    [RequestSizeLimit(100_000_000)]
    [Auditable("PurchaseOrderDocument", "Upload", CaptureBody = false)]
    public async Task<ActionResult<object>> Upload(
        long poId,
        [FromForm] IFormFile file,
        [FromForm] string category,
        [FromForm] string? documentNumber = null,
        [FromForm] decimal? amount = null)
    {
        if (file is null || file.Length == 0) return BadRequest(new { message = "No file uploaded." });
        if (!CategoryFolders.TryGetValue(category ?? string.Empty, out var folder))
            return BadRequest(new { message = $"Invalid category. Valid values: {string.Join(", ", CategoryFolders.Keys)}." });
        if (amount < 0) return BadRequest(new { message = "Document amount cannot be negative." });

        var po = await db.Set<PurchaseOrder>().Include(p => p.Supplier)
            .FirstOrDefaultAsync(p => p.Id == poId && p.Origin == "Stock");
        if (po is null) return NotFound();
        if (!TryGetUserId(out var userId)) return Unauthorized();

        string storedName;
        await using (var stream = file.OpenReadStream())
            storedName = storage.SaveFileInSupplierCategory(po.PONumber, po.Supplier.Name, folder, file.FileName, stream);

        var document = new PurchaseOrderDocument
        {
            POId = po.Id,
            Category = CategoryFolders.Keys.First(k => string.Equals(k, category, StringComparison.OrdinalIgnoreCase)),
            FileName = Path.GetFileName(file.FileName),
            StoredName = storedName,
            DocumentNumber = Clean(documentNumber),
            Amount = amount,
            UploadedBy = userId,
            CreatedAt = DateTime.UtcNow,
        };

        try
        {
            db.Set<PurchaseOrderDocument>().Add(document);
            if (document.Category == PurchaseOrderDocumentCategories.SupplierPI && document.DocumentNumber is not null)
                po.SupplierPIRef = document.DocumentNumber;
            await db.SaveChangesAsync();
        }
        catch
        {
            storage.DeleteFileInSupplierCategory(po.PONumber, po.Supplier.Name, folder, storedName);
            throw;
        }

        var warning = AmountWarning(document.Amount, po.TotalAmount);
        return CreatedAtAction(nameof(Download), new { poId, documentId = document.Id }, new
        {
            document = Map(document, User.Identity?.Name ?? string.Empty),
            warning,
        });
    }

    [HttpGet("{documentId:long}/download")]
    public async Task<IActionResult> Download(long poId, long documentId)
    {
        var row = await LoadAsync(poId, documentId);
        if (row is null) return NotFound();
        var (document, po) = row.Value;
        if (!CategoryFolders.TryGetValue(document.Category, out var folder)) return NotFound();
        var result = storage.OpenFileInSupplierCategory(po.PONumber, po.Supplier.Name, folder, document.StoredName);
        if (result is null) return NotFound();
        return File(result.Value.Stream, GetContentType(result.Value.AbsolutePath), document.FileName);
    }

    [HttpDelete("{documentId:long}")]
    [Authorize(Roles = "Admin,SuperAdmin,Expert,Inventory")]
    [Auditable("PurchaseOrderDocument", "Delete")]
    public async Task<IActionResult> Delete(long poId, long documentId)
    {
        var row = await LoadAsync(poId, documentId);
        if (row is null) return NotFound();
        var (document, po) = row.Value;
        if (!User.IsInRole("Admin") && !User.IsInRole("SuperAdmin")
            && (!TryGetUserId(out var userId) || document.UploadedBy != userId))
            return Forbid();

        db.Set<PurchaseOrderDocument>().Remove(document);
        if (document.Category == PurchaseOrderDocumentCategories.SupplierPI
            && string.Equals(po.SupplierPIRef, document.DocumentNumber, StringComparison.OrdinalIgnoreCase))
        {
            po.SupplierPIRef = await db.Set<PurchaseOrderDocument>()
                .Where(d => d.POId == poId && d.Id != documentId
                         && d.Category == PurchaseOrderDocumentCategories.SupplierPI
                         && d.DocumentNumber != null)
                .OrderByDescending(d => d.CreatedAt)
                .Select(d => d.DocumentNumber)
                .FirstOrDefaultAsync();
        }
        await db.SaveChangesAsync();

        if (CategoryFolders.TryGetValue(document.Category, out var folder))
            storage.DeleteFileInSupplierCategory(po.PONumber, po.Supplier.Name, folder, document.StoredName);
        return NoContent();
    }

    private Task<bool> IsStockPoAsync(long poId) => db.Set<PurchaseOrder>()
        .AnyAsync(p => p.Id == poId && p.Origin == "Stock");

    private async Task<(PurchaseOrderDocument Document, PurchaseOrder PO)?> LoadAsync(long poId, long documentId)
    {
        var document = await db.Set<PurchaseOrderDocument>()
            .FirstOrDefaultAsync(d => d.Id == documentId && d.POId == poId);
        if (document is null) return null;
        var po = await db.Set<PurchaseOrder>().Include(p => p.Supplier)
            .FirstOrDefaultAsync(p => p.Id == poId && p.Origin == "Stock");
        return po is null ? null : (document, po);
    }

    private bool TryGetUserId(out long userId)
        => long.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out userId);

    private static PurchaseOrderDocumentResponse Map(PurchaseOrderDocument document, string userName) => new()
    {
        Id = document.Id,
        POId = document.POId,
        Category = document.Category,
        FileName = document.FileName,
        DocumentNumber = document.DocumentNumber,
        Amount = document.Amount,
        UploadedBy = document.UploadedBy,
        UploadedByName = userName,
        CreatedAt = document.CreatedAt,
    };

    private static string? AmountWarning(decimal? documentAmount, decimal? poTotal)
        => documentAmount.HasValue && poTotal.HasValue
           && decimal.Round(documentAmount.Value, 2) != decimal.Round(poTotal.Value, 2)
            ? $"Supplier PI amount ({documentAmount.Value:0.00}) does not match PO total ({poTotal.Value:0.00})."
            : null;

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string GetContentType(string path)
    {
        var provider = new FileExtensionContentTypeProvider();
        return provider.TryGetContentType(path, out var contentType)
            ? contentType
            : "application/octet-stream";
    }
}
