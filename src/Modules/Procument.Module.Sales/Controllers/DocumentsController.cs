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

[ApiController]
[Route("api/documents")]
[Authorize(Roles = "Admin,SuperAdmin,Expert,Payment")]
public class DocumentsController : ControllerBase
{
    private readonly DbContext _db;
    private readonly IDocumentStorageService _storage;

    // Maps category key → folder name for PI-level documents
    private static readonly Dictionary<string, string> PiCategoryFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        ["customer_pop"] = "Customer POP",
        ["customer_po"] = "Customer PO",
        ["our_pi"] = "Our PI",
        ["quote"] = "Quote",
    };

    // Maps category key → folder name for supplier-level documents
    private static readonly Dictionary<string, string> SupplierCategoryFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        ["supplier_invoice"] = "Supplier Invoice",
        ["supplier_bank_info"] = "Supplier Bank Info",
        ["our_pop"] = "Our POP to Supplier",
        ["dp"] = "DP",
        // PO PDFs auto-saved by PdfController.GeneratePo land here.
        ["po"] = "PO",
    };

    public DocumentsController(DbContext db, IDocumentStorageService storage)
    {
        _db = db;
        _storage = storage;
    }

    // ───────────── List ─────────────

    [HttpGet("proforma-invoice/{invoiceId:long}")]
    public async Task<IActionResult> List(long invoiceId)
    {
        var invoice = await _db.Set<Invoice>().FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (invoice == null) return NotFound();

        var suppliers = await GetSuppliersForInvoiceAsync(invoiceId);

        var piCategoryPairs = PiCategoryFolders.Select(kv => (kv.Key, kv.Value));
        var piFiles = _storage.ListFilesInInvoiceCategories(invoice.InvoiceNumber, piCategoryPairs).ToList();

        var supplierCategoryPairs = SupplierCategoryFolders.Select(kv => (kv.Key, kv.Value));
        var supplierSections = suppliers
            .Select(s => new
            {
                supplierId = s.Id,
                supplierName = s.Name,
                files = _storage.ListFilesInSupplierCategories(invoice.InvoiceNumber, s.Name, supplierCategoryPairs).ToList()
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

    // ───────────── Customer POP with Amount ─────────────

    /// <summary>Upload a Customer POP with a payment amount. Auto-marks the invoice Paid when total paid reaches invoice total.</summary>
    [HttpPost("proforma-invoice/{invoiceId:long}/customer-pop")]
    [RequestSizeLimit(100_000_000)]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> UploadCustomerPop(
        long invoiceId,
        [FromForm] IFormFile file,
        [FromForm] decimal amount,
        [FromForm] string? notes = null)
    {
        if (file == null || file.Length == 0) return BadRequest("No file uploaded.");
        if (amount <= 0) return BadRequest("Amount must be greater than zero.");

        var invoice = await _db.Set<Invoice>().FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (invoice == null) return NotFound();

        using var stream = file.OpenReadStream();
        var savedName = _storage.SaveFileInInvoiceCategory(invoice.InvoiceNumber, "Customer POP", file.FileName, stream);

        var payment = new CustomerPayment
        {
            InvoiceId = invoiceId,
            FileName = savedName,
            Amount = amount,
            Notes = notes,
            CreatedAt = DateTime.UtcNow,
        };
        _db.Set<CustomerPayment>().Add(payment);
        await _db.SaveChangesAsync();

        var totalPaid = await _db.Set<CustomerPayment>()
            .Where(p => p.InvoiceId == invoiceId)
            .SumAsync(p => p.Amount);

        bool justPaid = false;
        if (totalPaid >= invoice.TotalAmount && invoice.Status != "Finish")
        {
            invoice.Status = "Finish";
            invoice.PaidDate = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            justPaid = true;
        }

        return Ok(new
        {
            fileName = savedName,
            amount,
            totalPaid,
            invoiceTotal = invoice.TotalAmount,
            isPaid = totalPaid >= invoice.TotalAmount,
            justPaid,
        });
    }

    /// <summary>
    /// All Customer Payment records across every proforma invoice, grouped by customer.
    /// Used by the Payment / SuperAdmin "Customer Payments" overview page.
    /// </summary>
    [HttpGet("customer-payments/all")]
    [Authorize(Roles = "SuperAdmin,Payment")]
    public async Task<IActionResult> GetAllCustomerPayments()
    {
        // Pull every payment + its invoice + customer in one trip
        var rows = await (
            from cp in _db.Set<CustomerPayment>()
            join inv in _db.Set<Invoice>() on cp.InvoiceId equals inv.Id
            join c in _db.Set<Customer>() on inv.CustomerId equals c.Id into cj
            from c in cj.DefaultIfEmpty()
            orderby cp.CreatedAt descending
            select new
            {
                cp.Id,
                cp.FileName,
                cp.Amount,
                cp.Notes,
                cp.CreatedAt,
                InvoiceId = inv.Id,
                InvoiceNumber = inv.InvoiceNumber,
                InvoiceTotal = inv.TotalAmount,
                InvoiceStatus = inv.Status,
                CustomerId = c != null ? c.Id : (long?)null,
                CustomerName = c != null ? c.CustomerCode : null,
            }
        ).ToListAsync();

        // Group by customer for the UI (one card per customer with all their payments inside)
        var groups = rows
            .GroupBy(r => new { r.CustomerId, r.CustomerName })
            .Select(g => new
            {
                customerId = g.Key.CustomerId,
                customerName = g.Key.CustomerName ?? "Unknown",
                totalPaid = g.Sum(x => x.Amount),
                paymentCount = g.Count(),
                invoiceCount = g.Select(x => x.InvoiceId).Distinct().Count(),
                payments = g.Select(x => new
                {
                    x.Id,
                    x.FileName,
                    x.Amount,
                    x.Notes,
                    x.CreatedAt,
                    x.InvoiceId,
                    x.InvoiceNumber,
                    x.InvoiceTotal,
                    x.InvoiceStatus,
                }).ToList(),
            })
            .OrderByDescending(g => g.totalPaid)
            .ToList();

        return Ok(new
        {
            customers = groups,
            totalCustomers = groups.Count,
            totalPayments = rows.Count,
            grandTotal = rows.Sum(r => r.Amount),
        });
    }

    /// <summary>Get customer payment records for a Proforma Invoice.</summary>
    [HttpGet("proforma-invoice/{invoiceId:long}/customer-payments")]
    public async Task<IActionResult> GetCustomerPayments(long invoiceId)
    {
        var invoice = await _db.Set<Invoice>().FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (invoice == null) return NotFound();

        var payments = await _db.Set<CustomerPayment>()
            .Where(p => p.InvoiceId == invoiceId)
            .OrderBy(p => p.CreatedAt)
            .Select(p => new { p.Id, p.FileName, p.Amount, p.Notes, p.CreatedAt })
            .ToListAsync();

        var totalPaid = payments.Sum(p => p.Amount);

        return Ok(new
        {
            payments,
            totalPaid,
            invoiceTotal = invoice.TotalAmount,
            isPaid = totalPaid >= invoice.TotalAmount,
        });
    }

    [HttpPost("proforma-invoice/{invoiceId:long}/upload")]
    [RequestSizeLimit(100_000_000)]
    public async Task<IActionResult> UploadPI(long invoiceId, [FromForm] IFormFile file, [FromForm] string? category = null)
    {
        if (file == null || file.Length == 0) return BadRequest("No file uploaded.");
        var invoice = await _db.Set<Invoice>().FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (invoice == null) return NotFound();

        var isAdminUser = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
        if (!isAdminUser) return Forbid();

        if (string.IsNullOrWhiteSpace(category) || !PiCategoryFolders.TryGetValue(category, out var categoryFolder))
            return BadRequest("Invalid or missing category. Valid values: " + string.Join(", ", PiCategoryFolders.Keys));

        using var stream = file.OpenReadStream();
        var savedName = _storage.SaveFileInInvoiceCategory(invoice.InvoiceNumber, categoryFolder, file.FileName, stream);

        return Ok(new { fileName = savedName });
    }

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

        if (string.IsNullOrWhiteSpace(category) || !SupplierCategoryFolders.TryGetValue(category, out var categoryFolder))
            return BadRequest("Invalid or missing category. Valid values: " + string.Join(", ", SupplierCategoryFolders.Keys));

        var isAdminUser = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
        if (!isAdminUser)
        {
            var allowedForUser = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "supplier_invoice", "supplier_bank_info"
            };
            if (!allowedForUser.Contains(category))
                return Forbid();
        }

        // Handle "final" POP suffix
        string fileName = file.FileName;
        if (isFinal && string.Equals(category, "our_pop", StringComparison.OrdinalIgnoreCase))
        {
            var ext = Path.GetExtension(fileName);
            var nameOnly = Path.GetFileNameWithoutExtension(fileName);
            if (!nameOnly.EndsWith("_final", StringComparison.OrdinalIgnoreCase))
            {
                fileName = nameOnly + "_final" + ext;
            }
        }

        // Buffer file so we can fan-out to multiple PIs
        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms);
            bytes = ms.ToArray();
        }

        var piNumbers = await GetInvoiceNumbersSharedWithSupplierAsync(invoiceId, supplierId);
        piNumbers.Add(invoice.InvoiceNumber);

        string savedName = string.Empty;
        var written = new List<string>();
        foreach (var piNumber in piNumbers.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            using var ms = new MemoryStream(bytes, writable: false);
            var fn = _storage.SaveFileInSupplierCategory(piNumber, supplier.Name, categoryFolder, fileName, ms);
            if (string.Equals(piNumber, invoice.InvoiceNumber, StringComparison.OrdinalIgnoreCase))
                savedName = fn;
            written.Add(piNumber);
        }

        // Update POItem statuses based on upload category
        if (category is "supplier_invoice" or "our_pop")
        {
            var newStatus = category == "supplier_invoice" ? "Document Added" : "Payment Done";
            var itemsToUpdate = await _db.Set<POItem>()
                .Include(i => i.PurchaseOrder)
                .Where(i => i.SupplierId == supplierId && i.ReturnedAt == null &&
                            (i.POId == null || i.PurchaseOrder!.InvoiceId == invoiceId ||
                             _db.Set<InvoiceItem>().Any(ii => ii.Id == i.InvoiceItemId && ii.InvoiceId == invoiceId)))
                .ToListAsync();

            var poIds = new HashSet<long>();
            foreach (var item in itemsToUpdate)
            {
                item.Status = newStatus;
                if (item.POId.HasValue) poIds.Add(item.POId.Value);
            }

            if (poIds.Count > 0)
            {
                var pos = await _db.Set<PurchaseOrder>().Where(p => poIds.Contains(p.Id)).ToListAsync();
                foreach (var po in pos)
                {
                    po.Status = newStatus;
                }
            }

            await _db.SaveChangesAsync();
        }

        return Ok(new { fileName = savedName, fannedOutToInvoices = written });
    }

    // ───────────── Download ─────────────

    [HttpGet("proforma-invoice/{invoiceId:long}/file")]
    public async Task<IActionResult> DownloadPI(long invoiceId, [FromQuery] string name, [FromQuery] string? category = null)
    {
        var invoice = await _db.Set<Invoice>().FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (invoice == null) return NotFound();

        if (!string.IsNullOrWhiteSpace(category) && PiCategoryFolders.TryGetValue(category, out var categoryFolder))
        {
            var result = _storage.OpenFileInInvoiceCategory(invoice.InvoiceNumber, categoryFolder, name);
            if (result == null) return NotFound();
            return File(result.Value.Stream, GetContentType(result.Value.AbsolutePath), name);
        }
        else
        {
            // Legacy fallback: flat folder
            var result = _storage.OpenProformaInvoiceFile(invoice.InvoiceNumber, name);
            if (result == null) return NotFound();
            return File(result.Value.Stream, GetContentType(result.Value.AbsolutePath), Path.GetFileName(result.Value.AbsolutePath));
        }
    }

    [HttpGet("proforma-invoice/{invoiceId:long}/supplier/{supplierId:long}/file")]
    public async Task<IActionResult> DownloadSupplier(long invoiceId, long supplierId, [FromQuery] string name, [FromQuery] string? category = null)
    {
        var invoice = await _db.Set<Invoice>().FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (invoice == null) return NotFound();
        var supplier = await _db.Set<Supplier>().FirstOrDefaultAsync(s => s.Id == supplierId);
        if (supplier == null) return NotFound();

        // Update status to "Waiting For Shipment" on download of critical documents
        if (category is "supplier_invoice" or "our_pop")
        {
            var itemsToUpdate = await _db.Set<POItem>()
                .Include(i => i.PurchaseOrder)
                .Where(i => i.SupplierId == supplierId && i.ReturnedAt == null &&
                            (i.POId == null || i.PurchaseOrder!.InvoiceId == invoiceId ||
                             _db.Set<InvoiceItem>().Any(ii => ii.Id == i.InvoiceItemId && ii.InvoiceId == invoiceId)))
                .ToListAsync();

            var poIds = new HashSet<long>();
            foreach (var item in itemsToUpdate)
            {
                item.Status = "Waiting For Shipment";
                if (item.POId.HasValue) poIds.Add(item.POId.Value);
            }

            if (poIds.Count > 0)
            {
                var pos = await _db.Set<PurchaseOrder>().Where(p => poIds.Contains(p.Id)).ToListAsync();
                foreach (var po in pos)
                {
                    po.Status = "Waiting For Shipment";
                }
            }

            await _db.SaveChangesAsync();
        }

        if (!string.IsNullOrWhiteSpace(category) && SupplierCategoryFolders.TryGetValue(category, out var categoryFolder))
        {
            var result = _storage.OpenFileInSupplierCategory(invoice.InvoiceNumber, supplier.Name, categoryFolder, name);
            if (result == null) return NotFound();
            return File(result.Value.Stream, GetContentType(result.Value.AbsolutePath), name);
        }
        else
        {
            // Legacy fallback: flat folder
            var result = _storage.OpenSupplierFile(invoice.InvoiceNumber, supplier.Name, name);
            if (result == null) return NotFound();
            return File(result.Value.Stream, GetContentType(result.Value.AbsolutePath), Path.GetFileName(result.Value.AbsolutePath));
        }
    }

    // ───────────── Delete ─────────────

    [HttpDelete("proforma-invoice/{invoiceId:long}/file")]
    public async Task<IActionResult> DeletePI(long invoiceId, [FromQuery] string name, [FromQuery] string? category = null)
    {
        var invoice = await _db.Set<Invoice>().FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (invoice == null) return NotFound();

        bool ok;
        if (!string.IsNullOrWhiteSpace(category) && PiCategoryFolders.TryGetValue(category, out var categoryFolder))
            ok = _storage.DeleteFileInInvoiceCategory(invoice.InvoiceNumber, categoryFolder, name);
        else
            ok = _storage.DeleteProformaInvoiceFile(invoice.InvoiceNumber, name);

        return ok ? Ok() : NotFound();
    }

    [HttpDelete("proforma-invoice/{invoiceId:long}/supplier/{supplierId:long}/file")]
    public async Task<IActionResult> DeleteSupplier(long invoiceId, long supplierId, [FromQuery] string name, [FromQuery] string? category = null)
    {
        var invoice = await _db.Set<Invoice>().FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (invoice == null) return NotFound();
        var supplier = await _db.Set<Supplier>().FirstOrDefaultAsync(s => s.Id == supplierId);
        if (supplier == null) return NotFound();

        var piNumbers = await GetInvoiceNumbersSharedWithSupplierAsync(invoiceId, supplierId);
        piNumbers.Add(invoice.InvoiceNumber);

        foreach (var piNumber in piNumbers.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(category) && SupplierCategoryFolders.TryGetValue(category, out var categoryFolder))
                _storage.DeleteFileInSupplierCategory(piNumber, supplier.Name, categoryFolder, name);
            else
                _storage.DeleteSupplierFile(piNumber, supplier.Name, name);
        }

        return Ok();
    }

    // ───────────── Helpers ─────────────

    private async Task<List<PurchaseOrder>> GetPOsForInvoiceAndSupplierAsync(long invoiceId, long supplierId)
    {
        return await _db.Set<PurchaseOrder>()
            .Where(p => p.SupplierId == supplierId
                        && (p.InvoiceId == invoiceId
                            || p.POItems.Any(i => i.InvoiceItemId.HasValue
                                                  && _db.Set<InvoiceItem>().Any(ii => ii.Id == i.InvoiceItemId
                                                                                      && ii.InvoiceId == invoiceId))))
            .ToListAsync();
    }

    private async Task<List<Supplier>> GetSuppliersForInvoiceAsync(long invoiceId)
    {
        var directSupplierIds = await _db.Set<PurchaseOrder>()
            .Where(p => p.InvoiceId == invoiceId)
            .Select(p => p.SupplierId)
            .ToListAsync();

        var indirectSupplierIds = await _db.Set<POItem>()
            .Where(i => i.InvoiceItemId.HasValue
                        && i.POId != null
                        && _db.Set<InvoiceItem>().Any(ii => ii.Id == i.InvoiceItemId && ii.InvoiceId == invoiceId))
            .Select(i => i.PurchaseOrder!.SupplierId)
            .ToListAsync();

        var ids = directSupplierIds.Concat(indirectSupplierIds).Distinct().ToList();
        return await _db.Set<Supplier>().Where(s => ids.Contains(s.Id)).ToListAsync();
    }

    private async Task<HashSet<string>> GetInvoiceNumbersSharedWithSupplierAsync(long invoiceId, long supplierId)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var poIds = await _db.Set<PurchaseOrder>()
            .Where(p => p.SupplierId == supplierId
                        && (p.InvoiceId == invoiceId
                            || p.POItems.Any(i => i.InvoiceItemId.HasValue
                                                  && _db.Set<InvoiceItem>().Any(ii => ii.Id == i.InvoiceItemId && ii.InvoiceId == invoiceId))))
            .Select(p => p.Id)
            .ToListAsync();

        if (poIds.Count == 0) return result;

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

    private static string GetContentType(string path)
    {
        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(path, out var contentType))
            contentType = "application/octet-stream";
        return contentType;
    }
}
