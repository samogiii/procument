using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Catalog.Entities;
using Procument.Module.Purchasing.Entities;
using Procument.Module.Sales.Entities;
using Procument.Shared.Services;

namespace Procument.Module.Sales.Controllers;

/// <summary>
/// Document storage endpoints for Proforma Invoices and per-Supplier subfolders.
/// </summary>
[ApiController]
[Route("api/documents")]
[Authorize(Roles = "Admin,SuperAdmin,Expert,Payment")]
public class DocumentsController : ControllerBase
{
    private readonly DbContext _db;
    private readonly IDocumentStorageService _storage;

    public DocumentsController(DbContext db, IDocumentStorageService storage)
    {
        _db = db;
        _storage = storage;
    }

    // ───────────── List ─────────────

    /// <summary>List documents for a Proforma Invoice: PI-level files + each supplier (suppliers come from POs linked to the PI).</summary>
    [HttpGet("proforma-invoice/{invoiceId:long}")]
    public async Task<IActionResult> List(long invoiceId)
    {
        var invoice = await _db.Set<Invoice>().FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (invoice == null) return NotFound();

        var suppliers = await GetSuppliersForInvoiceAsync(invoiceId);

        var piFiles = _storage.ListProformaInvoiceFiles(invoice.InvoiceNumber).ToList();

        var supplierSections = suppliers
            .Select(s => new
            {
                supplierId = s.Id,
                supplierName = s.Name,
                files = _storage.ListSupplierFiles(invoice.InvoiceNumber, s.Name).ToList()
            })
            .ToList();

        return Ok(new
        {
            invoiceId,
            invoiceNumber = invoice.InvoiceNumber,
            piFiles,
            suppliers = supplierSections
        });
    }

    // ───────────── Upload ─────────────

    /// <summary>Upload a PI-level document.</summary>
    [HttpPost("proforma-invoice/{invoiceId:long}/upload")]
    [RequestSizeLimit(100_000_000)]
    public async Task<IActionResult> UploadPI(long invoiceId, [FromForm] IFormFile file, [FromForm] string? category = null)
    {
        if (file == null || file.Length == 0) return BadRequest("No file uploaded.");
        var invoice = await _db.Set<Invoice>().FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (invoice == null) return NotFound();

        var existingFiles = _storage.ListProformaInvoiceFiles(invoice.InvoiceNumber).ToList();
        var fileName = GenerateNumberedFileName(existingFiles, category, file.FileName);
        
        using var stream = file.OpenReadStream();
        _storage.SaveProformaInvoiceFile(invoice.InvoiceNumber, fileName, stream);

        return Ok(new { fileName });
    }

    /// <summary>
    /// Upload a supplier-level document (our_pop / supplier_invoice / generic).
    /// Fans out to every PI whose POs share this supplier.
    /// </summary>
    [HttpPost("proforma-invoice/{invoiceId:long}/supplier/{supplierId:long}/upload")]
    [RequestSizeLimit(100_000_000)]
    public async Task<IActionResult> UploadSupplier(
        long invoiceId, 
        long supplierId, 
        [FromForm] IFormFile file, 
        [FromForm] string? category = null,
        [FromForm] bool isFinal = false)
    {
        if (file == null || file.Length == 0) return BadRequest("No file uploaded.");
        var invoice = await _db.Set<Invoice>().FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (invoice == null) return NotFound();
        var supplier = await _db.Set<Supplier>().FirstOrDefaultAsync(s => s.Id == supplierId);
        if (supplier == null) return NotFound("Supplier not found.");

        var existingFiles = _storage.ListSupplierFiles(invoice.InvoiceNumber, supplier.Name).ToList();
        
        if (category == "our_pop" && existingFiles.Any(f => f.Name.Contains("Our POP") && f.Name.Contains("_final")))
        {
            return BadRequest("A Final POP has already been uploaded for this supplier.");
        }

        var fileName = GenerateNumberedFileName(existingFiles, category, file.FileName, isFinal);

        // Read the file fully into memory so we can write it to multiple targets
        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms);
            bytes = ms.ToArray();
        }

        // Find every PI that shares a PO with this supplier and the primary PI (fan-out)
        var piNumbers = await GetInvoiceNumbersSharedWithSupplierAsync(invoiceId, supplierId);
        piNumbers.Add(invoice.InvoiceNumber); // always include the primary target

        var written = new List<string>();
        foreach (var piNumber in piNumbers.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            using var ms = new MemoryStream(bytes, writable: false);
            _storage.SaveSupplierFile(piNumber, supplier.Name, fileName, ms);
            written.Add(piNumber);
        }

        return Ok(new { fileName, fannedOutToInvoices = written });
    }

    private string GenerateNumberedFileName(IEnumerable<DocumentFileInfo> existingFiles, string? category, string originalFileName, bool isFinal = false)
    {
        var ext = Path.GetExtension(originalFileName);
        
        if (string.IsNullOrWhiteSpace(category))
        {
            return Path.GetFileNameWithoutExtension(originalFileName) + "_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + ext;
        }

        string baseDisplayName = category switch
        {
            "our_pop" => "Our POP",
            "supplier_invoice" => "Supplier Invoice",
            "customer_pop" => "Customer POP",
            "customer_po" => "Customer PO",
            "our_pi" => "Our PI",
            "quote" => "Quote",
            "supplier_bank_info" => "Supplier Bank Info",
            "dp" => "DP",
            _ => category.Replace("_", " ")
        };

        var prefix = $"{baseDisplayName} number";
        
        // Count how many files match this specific pattern: "DisplayName number X"
        var count = existingFiles.Count(f => f.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        var nextNumber = count + 1;
        
        string fileName = $"{prefix} {nextNumber}";
        if (isFinal) fileName += "_final";
        return fileName + ext;
    }

    // ───────────── Download ─────────────

    /// <summary>Download a PI-level document.</summary>
    [HttpGet("proforma-invoice/{invoiceId:long}/file")]
    public async Task<IActionResult> DownloadPI(long invoiceId, [FromQuery] string name)
    {
        var invoice = await _db.Set<Invoice>().FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (invoice == null) return NotFound();

        var result = _storage.OpenProformaInvoiceFile(invoice.InvoiceNumber, name);
        if (result == null) return NotFound();
        var contentType = GetContentType(result.Value.AbsolutePath);
        return File(result.Value.Stream, contentType, Path.GetFileName(result.Value.AbsolutePath));
    }

    /// <summary>Download a supplier-level document.</summary>
    [HttpGet("proforma-invoice/{invoiceId:long}/supplier/{supplierId:long}/file")]
    public async Task<IActionResult> DownloadSupplier(long invoiceId, long supplierId, [FromQuery] string name)
    {
        var invoice = await _db.Set<Invoice>().FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (invoice == null) return NotFound();
        var supplier = await _db.Set<Supplier>().FirstOrDefaultAsync(s => s.Id == supplierId);
        if (supplier == null) return NotFound();

        var result = _storage.OpenSupplierFile(invoice.InvoiceNumber, supplier.Name, name);
        if (result == null) return NotFound();
        var contentType = GetContentType(result.Value.AbsolutePath);
        return File(result.Value.Stream, contentType, Path.GetFileName(result.Value.AbsolutePath));
    }

    // ───────────── Delete ─────────────

    [HttpDelete("proforma-invoice/{invoiceId:long}/file")]
    public async Task<IActionResult> DeletePI(long invoiceId, [FromQuery] string name)
    {
        var invoice = await _db.Set<Invoice>().FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (invoice == null) return NotFound();
        var ok = _storage.DeleteProformaInvoiceFile(invoice.InvoiceNumber, name);
        return ok ? Ok() : NotFound();
    }

    [HttpDelete("proforma-invoice/{invoiceId:long}/supplier/{supplierId:long}/file")]
    public async Task<IActionResult> DeleteSupplier(long invoiceId, long supplierId, [FromQuery] string name)
    {
        var invoice = await _db.Set<Invoice>().FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (invoice == null) return NotFound();
        var supplier = await _db.Set<Supplier>().FirstOrDefaultAsync(s => s.Id == supplierId);
        if (supplier == null) return NotFound();
        var ok = _storage.DeleteSupplierFile(invoice.InvoiceNumber, supplier.Name, name);
        return ok ? Ok() : NotFound();
    }

    // ───────────── Helpers ─────────────

    /// <summary>Get the distinct suppliers linked to a PI (via POs whose POItems reference the PI's InvoiceItems, plus POs with po.InvoiceId = piId).</summary>
    private async Task<List<Supplier>> GetSuppliersForInvoiceAsync(long invoiceId)
    {
        // Supplier IDs from POs that reference this invoice directly
        var directSupplierIds = await _db.Set<PurchaseOrder>()
            .Where(p => p.InvoiceId == invoiceId)
            .Select(p => p.SupplierId)
            .ToListAsync();

        // Supplier IDs from POs whose POItems reference this invoice's items
        var indirectSupplierIds = await _db.Set<POItem>()
            .Where(i => i.InvoiceItemId.HasValue
                        && i.POId != null
                        && _db.Set<InvoiceItem>().Any(ii => ii.Id == i.InvoiceItemId && ii.InvoiceId == invoiceId))
            .Select(i => i.PurchaseOrder!.SupplierId)
            .ToListAsync();

        var ids = directSupplierIds.Concat(indirectSupplierIds).Distinct().ToList();
        return await _db.Set<Supplier>().Where(s => ids.Contains(s.Id)).ToListAsync();
    }

    /// <summary>
    /// For fan-out: given a PI and a supplier, find the set of Invoice Numbers that share a PO with this supplier.
    /// Works by walking: PI → POs (supplier = supplierId) → all InvoiceItems of those POs → their Invoices.
    /// </summary>
    private async Task<HashSet<string>> GetInvoiceNumbersSharedWithSupplierAsync(long invoiceId, long supplierId)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // POs for this supplier that are connected to this PI (directly or via an item)
        var poIds = await _db.Set<PurchaseOrder>()
            .Where(p => p.SupplierId == supplierId
                        && (p.InvoiceId == invoiceId
                            || p.POItems.Any(i => i.InvoiceItemId.HasValue
                                                  && _db.Set<InvoiceItem>().Any(ii => ii.Id == i.InvoiceItemId && ii.InvoiceId == invoiceId))))
            .Select(p => p.Id)
            .ToListAsync();

        if (poIds.Count == 0) return result;

        // Collect ALL PIs referenced by those POs (direct + via items)
        var directInvoiceIds = await _db.Set<PurchaseOrder>()
            .Where(p => poIds.Contains(p.Id) && p.InvoiceId.HasValue)
            .Select(p => p.InvoiceId!.Value)
            .ToListAsync();

        var indirectInvoiceIds = await _db.Set<POItem>()
            .Where(i => i.POId.HasValue && poIds.Contains(i.POId.Value) && i.InvoiceItemId.HasValue)
            .Select(i => _db.Set<InvoiceItem>().Where(ii => ii.Id == i.InvoiceItemId).Select(ii => ii.InvoiceId).FirstOrDefault())
            .ToListAsync();

        var allInvoiceIds = directInvoiceIds.Concat(indirectInvoiceIds).Distinct().ToList();
        var numbers = await _db.Set<Invoice>().Where(i => allInvoiceIds.Contains(i.Id)).Select(i => i.InvoiceNumber).ToListAsync();
        foreach (var n in numbers) if (!string.IsNullOrWhiteSpace(n)) result.Add(n);
        return result;
    }

    private static string BuildFileName(string? category, string originalName)
    {
        var ext = Path.GetExtension(originalName);
        var baseName = string.IsNullOrWhiteSpace(category)
            ? Path.GetFileNameWithoutExtension(originalName)
            : category.Trim();
        // Append a timestamp when no category is provided, to avoid name collisions
        if (string.IsNullOrWhiteSpace(category))
        {
            baseName += "_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        }
        return baseName + ext;
    }

    private static string GetContentType(string path)
    {
        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(path, out var contentType))
        {
            contentType = "application/octet-stream";
        }
        return contentType;
    }
}
