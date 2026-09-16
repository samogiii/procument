using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Catalog.Entities;
using Procument.Module.Purchasing.Entities;
using Procument.Module.Purchasing.Services;
using Procument.Module.Sales.Entities;
using Procument.Module.Sales.Services;
using Procument.Shared.Services;

namespace Procument.Module.Sales.Controllers;

[ApiController]
[Route("api/documents")]
[Authorize(Roles = "Admin,SuperAdmin,Expert,Payment")]
public class DocumentsController : ControllerBase
{
    private readonly DbContext _db;
    private readonly IDocumentStorageService _storage;
    private readonly IPaymentBoxService _paymentBoxService;

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
        ["end_user_document"] = "End User Document",
        ["our_pop"] = "Our POP to Supplier",
        ["dp"] = "DP",
        // PO PDFs auto-saved by PdfController.GeneratePo land here.
        ["po"] = "PO",
    };

    // Final invoice documents are kept in the same managed document store, under
    // the final invoice number.  This makes each final-invoice id self-contained
    // without mixing its generated documents with its source proforma invoice.
    private static readonly Dictionary<string, string> FinalInvoiceCategoryFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        ["invoice"] = "Invoice",
        ["packing_list"] = "Packing List",
    };

    public DocumentsController(DbContext db, IDocumentStorageService storage, IPaymentBoxService paymentBoxService)
    {
        _db = db;
        _storage = storage;
        _paymentBoxService = paymentBoxService;
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

    // ───────────── Final invoice documents ─────────────

    [HttpGet("final-invoice/{invoiceId:long}")]
    public async Task<IActionResult> ListFinalInvoiceDocuments(long invoiceId)
    {
        var invoice = await _db.Set<FinalInvoice>().FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (invoice == null) return NotFound();

        var categories = FinalInvoiceCategoryFolders.Select(kv => (kv.Key, kv.Value));
        return Ok(new
        {
            invoiceId,
            invoiceNumber = invoice.InvoiceNumber,
            files = _storage.ListFilesInInvoiceCategories(invoice.InvoiceNumber, categories).ToList(),
        });
    }

    [HttpPost("final-invoice/{invoiceId:long}/upload")]
    [RequestSizeLimit(100_000_000)]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> UploadFinalInvoiceDocument(
        long invoiceId,
        [FromForm] IFormFile file,
        [FromForm] string? category = null)
    {
        if (file == null || file.Length == 0) return BadRequest("No file uploaded.");
        var invoice = await _db.Set<FinalInvoice>().FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (invoice == null) return NotFound();
        if (string.IsNullOrWhiteSpace(category) || !FinalInvoiceCategoryFolders.TryGetValue(category, out var categoryFolder))
            return BadRequest("Invalid or missing category. Valid values: " + string.Join(", ", FinalInvoiceCategoryFolders.Keys));

        using var stream = file.OpenReadStream();
        var savedName = _storage.SaveFileInInvoiceCategory(invoice.InvoiceNumber, categoryFolder, file.FileName, stream);
        return Ok(new { fileName = savedName });
    }

    [HttpGet("final-invoice/{invoiceId:long}/file")]
    public async Task<IActionResult> DownloadFinalInvoiceDocument(long invoiceId, [FromQuery] string name, [FromQuery] string category)
    {
        var invoice = await _db.Set<FinalInvoice>().FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (invoice == null) return NotFound();
        if (!FinalInvoiceCategoryFolders.TryGetValue(category, out var categoryFolder)) return BadRequest("Invalid category.");

        var result = _storage.OpenFileInInvoiceCategory(invoice.InvoiceNumber, categoryFolder, name);
        return result == null
            ? NotFound()
            : File(result.Value.Stream, GetContentType(result.Value.AbsolutePath), Path.GetFileName(result.Value.AbsolutePath));
    }

    [HttpDelete("final-invoice/{invoiceId:long}/file")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> DeleteFinalInvoiceDocument(long invoiceId, [FromQuery] string name, [FromQuery] string category)
    {
        var invoice = await _db.Set<FinalInvoice>().FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (invoice == null) return NotFound();
        if (!FinalInvoiceCategoryFolders.TryGetValue(category, out var categoryFolder)) return BadRequest("Invalid category.");

        return _storage.DeleteFileInInvoiceCategory(invoice.InvoiceNumber, categoryFolder, name) ? Ok() : NotFound();
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
        [FromForm] string? notes = null,
        [FromForm] string? currency = null,
        [FromForm] decimal? exchangeRate = null,
        [FromForm] long? walletId = null)
    {
        if (file == null || file.Length == 0) return BadRequest("No file uploaded.");
        if (amount <= 0) return BadRequest("Amount must be greater than zero.");

        var normalizedCurrency = string.IsNullOrWhiteSpace(currency)
            ? "USD"
            : currency.Trim().ToUpperInvariant();

        var invoice = await _db.Set<Invoice>().FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (invoice == null) return NotFound();

        var selectedWalletId = invoice.DefaultDepositWalletId ?? walletId;
        if (!selectedWalletId.HasValue)
            return BadRequest(new
            {
                message = "Select the bank / deposit wallet for this POP."
            });

        var selectedWallet = await _db.Set<PaymentBox>()
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == selectedWalletId.Value);
        if (selectedWallet == null)
            return BadRequest(new { message = "The selected bank / deposit wallet no longer exists. Select another wallet and try again." });

        var walletCurrency = selectedWallet.Currency.Trim().ToUpperInvariant();
        // A single rate can safely cover the two supported POP paths:
        //   USD POP -> foreign wallet: wallet units per USD (for example 7.20 CNY)
        //   foreign POP -> matching wallet: USD per received currency unit
        // Cross-converting two non-USD currencies would require two independent rates.
        if (normalizedCurrency != "USD" && normalizedCurrency != walletCurrency)
            return BadRequest(new
            {
                message = $"Choose USD or {walletCurrency} for a POP deposited into this wallet."
            });

        var needsExchangeRate = normalizedCurrency != "USD" || walletCurrency != "USD";
        if (needsExchangeRate && (!exchangeRate.HasValue || exchangeRate.Value <= 0))
            return BadRequest(new { message = $"An exchange rate is required between USD and {walletCurrency}." });

        // Customer deposits and PI payment progress are always stored in USD.
        var usdAmount = normalizedCurrency == "USD"
            ? amount
            : Math.Round(amount * exchangeRate!.Value, 2, MidpointRounding.AwayFromZero);
        if (usdAmount <= 0)
            return BadRequest(new { message = "The converted USD amount must be greater than zero." });

        // Wallet transactions use Amount * ExchangeRate as their wallet-currency value.
        // Same-currency deposits therefore use no factor; USD into a CNY wallet uses the
        // entered CNY-per-USD factor.
        decimal? walletExchangeRate = normalizedCurrency == walletCurrency ? null : exchangeRate;

        // A missing PI wallet can be selected directly in the POP dialog, including
        // after a Final Invoice has locked the rest of the PI. Existing selections
        // remain authoritative and cannot be replaced through the upload request.
        if (!invoice.DefaultDepositWalletId.HasValue)
        {
            invoice.DefaultDepositWalletId = selectedWallet.Id;
            await _db.SaveChangesAsync();
        }

        using var stream = file.OpenReadStream();
        var savedName = _storage.SaveFileInInvoiceCategory(invoice.InvoiceNumber, "Customer POP", file.FileName, stream);
        var invoiceNumber = invoice.InvoiceNumber;
        var executionStrategy = _db.Database.CreateExecutionStrategy();
        try
        {
            return await executionStrategy.ExecuteAsync<IActionResult>(async () =>
            {
                // A retry must start from a clean context. The POP filename is
                // stable across attempts and doubles as the idempotency key.
                _db.ChangeTracker.Clear();
                await using var transaction = await _db.Database.BeginTransactionAsync();

                var currentInvoice = await _db.Set<Invoice>().FirstOrDefaultAsync(i => i.Id == invoiceId);
                if (currentInvoice == null)
                    return NotFound();

                var existingPayment = await _db.Set<CustomerPayment>()
                    .FirstOrDefaultAsync(p => p.InvoiceId == invoiceId && p.FileName == savedName);
                if (existingPayment != null)
                {
                    var existingTotal = await _db.Set<CustomerPayment>()
                        .Where(p => p.InvoiceId == invoiceId)
                        .SumAsync(p => p.Amount);
                    await transaction.CommitAsync();
                    return Ok(new
                    {
                        fileName = savedName,
                        amount = existingPayment.Amount,
                        receivedAmount = existingPayment.ReceivedAmount ?? existingPayment.Amount,
                        currency = existingPayment.Currency ?? "USD",
                        exchangeRate = existingPayment.ExchangeRate,
                        totalPaid = existingTotal,
                        invoiceTotal = currentInvoice.TotalAmount,
                        isPaid = existingTotal >= currentInvoice.TotalAmount,
                        justPaid = false,
                        walletId = currentInvoice.DefaultDepositWalletId,
                    });
                }

                var payment = new CustomerPayment
                {
                    InvoiceId = invoiceId,
                    FileName = savedName,
                    Amount = usdAmount,
                    ReceivedAmount = amount,
                    Currency = normalizedCurrency,
                    ExchangeRate = normalizedCurrency == "USD" ? null : exchangeRate,
                    Notes = notes,
                    CreatedAt = DateTime.UtcNow,
                };
                _db.Set<CustomerPayment>().Add(payment);
                await _db.SaveChangesAsync();

                var deposited = await _paymentBoxService.TryAutoDepositAsync(
                    invoiceId, amount, currentInvoice.CustomerId, normalizedCurrency, walletExchangeRate,
                    currentInvoice.DefaultDepositWalletId, savedName);
                if (!deposited)
                {
                    await transaction.RollbackAsync();
                    _storage.DeleteFileInInvoiceCategory(invoiceNumber, "Customer POP", savedName);
                    return BadRequest(new
                    {
                        message = $"The {normalizedCurrency} payment could not be converted into the selected {walletCurrency} wallet. Check the exchange rate and try again."
                    });
                }

                var totalPaid = await _db.Set<CustomerPayment>()
                    .Where(p => p.InvoiceId == invoiceId)
                    .SumAsync(p => p.Amount);

                bool justPaid = false;
                if (totalPaid >= currentInvoice.TotalAmount && currentInvoice.Status != "Finish")
                {
                    currentInvoice.Status = "Finish";
                    currentInvoice.PaidDate = DateTime.UtcNow;
                    await _db.SaveChangesAsync();
                    justPaid = true;
                }

                await transaction.CommitAsync();
                return Ok(new
                {
                    fileName = savedName,
                    amount = usdAmount,
                    receivedAmount = amount,
                    currency = normalizedCurrency,
                    exchangeRate,
                    totalPaid,
                    invoiceTotal = currentInvoice.TotalAmount,
                    isPaid = totalPaid >= currentInvoice.TotalAmount,
                    justPaid,
                    walletId = currentInvoice.DefaultDepositWalletId,
                });
            });
        }
        catch
        {
            _storage.DeleteFileInInvoiceCategory(invoiceNumber, "Customer POP", savedName);
            throw;
        }
    }

    /// <summary>
    /// All Customer Payment records across every proforma invoice, grouped by customer.
    /// Used by the Payment / SuperAdmin "Customer Payments" overview page.
    /// Payment users only see customers whose Base is in their assigned bases.
    /// </summary>
    [HttpGet("customer-payments/all")]
    [Authorize(Roles = "SuperAdmin,Admin,Payment,AHM")]
    public async Task<IActionResult> GetAllCustomerPayments()
    {
        bool isAdmin = User.IsInRole("SuperAdmin") || User.IsInRole("Admin");
        var basesClaim = User.FindFirst("bases")?.Value ?? "";
        int[] userBases = basesClaim.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var b) ? b : -1)
            .Where(b => b > 0).ToArray();

        // Pull every payment + its invoice + customer in one trip.
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
                ReceivedAmount = cp.ReceivedAmount ?? cp.Amount,
                Currency = cp.Currency ?? "USD",
                cp.ExchangeRate,
                cp.Notes,
                cp.CreatedAt,
                InvoiceId = inv.Id,
                InvoiceNumber = inv.InvoiceNumber,
                InvoiceTotal = inv.TotalAmount,
                InvoiceStatus = inv.Status,
                CustomerId = c != null ? c.Id : (long?)null,
                CustomerName = c != null ? c.CustomerCode : null,
                CustomerBase = c != null ? c.Base : (int?)null,
            }
        ).ToListAsync();

        // For non-admin users, restrict to their assigned bases.
        // If a user has no bases assigned, they see nothing (empty list).
        if (!isAdmin && userBases.Length > 0)
            rows = rows.Where(r => r.CustomerBase.HasValue && userBases.Contains(r.CustomerBase.Value)).ToList();
        else if (!isAdmin)
            rows = [];

        // Payment Deposit is only for catalog customers.  Customer codes are
        // required there and must use the C-prefix (for example C101/C205).
        rows = rows.Where(r => !string.IsNullOrWhiteSpace(r.CustomerName)
            && r.CustomerName.StartsWith("C", StringComparison.OrdinalIgnoreCase)).ToList();

        // The payment history above contains only invoices that already have a POP.
        // The payment screen also needs invoices with no POP so it can show the real
        // outstanding amount due from each customer.
        var invoiceRows = await (
            from inv in _db.Set<Invoice>()
            join c in _db.Set<Customer>() on inv.CustomerId equals c.Id into cj
            from c in cj.DefaultIfEmpty()
            where !inv.IsCancelled
            select new
            {
                InvoiceId = inv.Id,
                InvoiceNumber = inv.InvoiceNumber,
                InvoiceTotal = inv.TotalAmount,
                PaymentTerm = inv.PaymentStatus,
                PrepaymentPercent = inv.PrepaymentPercent,
                CustomerName = c != null ? c.CustomerCode : null,
                CustomerBase = c != null ? c.Base : (int?)null,
            }
        ).ToListAsync();

        if (!isAdmin && userBases.Length > 0)
            invoiceRows = invoiceRows.Where(r => r.CustomerBase.HasValue && userBases.Contains(r.CustomerBase.Value)).ToList();
        else if (!isAdmin)
            invoiceRows = [];

        invoiceRows = invoiceRows.Where(r => !string.IsNullOrWhiteSpace(r.CustomerName)
            && r.CustomerName.StartsWith("C", StringComparison.OrdinalIgnoreCase)).ToList();

        var paidByInvoice = rows
            .GroupBy(r => r.InvoiceId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var invoices = invoiceRows.Select(r =>
        {
            var totalPaid = paidByInvoice.GetValueOrDefault(r.InvoiceId, 0m);
            var isPrepayment = string.Equals(r.PaymentTerm, "Prepayment", StringComparison.OrdinalIgnoreCase);
            var requiredAmount = isPrepayment
                ? r.InvoiceTotal * Math.Clamp(r.PrepaymentPercent ?? 100m, 0m, 100m) / 100m
                : r.InvoiceTotal;

            return new
            {
                r.InvoiceId,
                r.InvoiceNumber,
                r.CustomerName,
                r.CustomerBase,
                r.InvoiceTotal,
                r.PaymentTerm,
                r.PrepaymentPercent,
                requiredAmount,
                totalPaid,
                remainingAmount = Math.Max(0m, requiredAmount - totalPaid),
                isPrepayment,
            };
        }).OrderBy(r => r.remainingAmount == 0).ThenBy(r => r.CustomerName).ThenBy(r => r.InvoiceNumber).ToList();

        // Group by customer for the UI (one card per customer with all their payments inside)
        var groups = rows
            .GroupBy(r => new { r.CustomerId, r.CustomerName, r.CustomerBase })
            .Select(g => new
            {
                customerId = g.Key.CustomerId,
                customerName = g.Key.CustomerName ?? "Unknown",
                customerBase = g.Key.CustomerBase,
                totalPaid = g.Sum(x => x.Amount),
                paymentCount = g.Count(),
                invoiceCount = g.Select(x => x.InvoiceId).Distinct().Count(),
                payments = g.Select(x => new
                {
                    x.Id,
                    x.FileName,
                    x.Amount,
                    x.ReceivedAmount,
                    x.Currency,
                    x.ExchangeRate,
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
            invoices,
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
            .Select(p => new
            {
                p.Id,
                p.FileName,
                p.Amount,
                ReceivedAmount = p.ReceivedAmount ?? p.Amount,
                Currency = p.Currency ?? "USD",
                p.ExchangeRate,
                p.Notes,
                p.CreatedAt
            })
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

        if (string.Equals(category, "our_pop", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Upload supplier POPs from Payment so the PR amount and wallet are recorded together." });

        var isAdminUser = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
        if (!isAdminUser)
        {
            var allowedForUser = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "supplier_invoice", "supplier_bank_info", "end_user_document"
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

        // Supplier documents move only their PO and PI lines to the PR stage.
        if (category is "supplier_invoice" or "supplier_bank_info")
        {
            var newStatus = PurchaseOrderStatusFlow.WaitingForPr;
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

            await PurchaseOrderStatusFlow.SyncPiItemsAsync(_db, itemsToUpdate, newStatus);
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

        // The POP download is the hand-off from payment to shipment.
        if (category == "our_pop")
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
                item.Status = PurchaseOrderStatusFlow.WaitingForShipment;
                if (item.POId.HasValue) poIds.Add(item.POId.Value);
            }

            if (poIds.Count > 0)
            {
                var pos = await _db.Set<PurchaseOrder>().Where(p => poIds.Contains(p.Id)).ToListAsync();
                foreach (var po in pos)
                {
                    po.Status = PurchaseOrderStatusFlow.WaitingForShipment;
                }
            }

            await PurchaseOrderStatusFlow.SyncPiItemsAsync(
                _db, itemsToUpdate, PurchaseOrderStatusFlow.WaitingForShipment);

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

        // A Customer POP is both a document and a money record. Remove the PI
        // credit and its matching automatic wallet deposit as one operation.
        var isCustomerPop = string.Equals(category, "customer_pop", StringComparison.OrdinalIgnoreCase);
        var payment = isCustomerPop
            ? await _db.Set<CustomerPayment>().FirstOrDefaultAsync(p => p.InvoiceId == invoiceId && p.FileName == name)
            : null;

        if (payment != null)
        {
            var walletDeposits = await _db.Set<PaymentTransaction>()
                .Where(t => t.InvoiceId == invoiceId
                    && t.PopFileName == name
                    && t.IsAuto
                    && t.Type == "Deposit"
                    && t.FromType == "Customer")
                .ToListAsync();
            _db.Set<PaymentTransaction>().RemoveRange(walletDeposits);
            _db.Set<CustomerPayment>().Remove(payment);
            await _db.SaveChangesAsync();

            var remainingPaid = await _db.Set<CustomerPayment>()
                .Where(p => p.InvoiceId == invoiceId)
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;
            if (invoice.Status == "Finish" && remainingPaid < invoice.TotalAmount)
            {
                invoice.Status = string.Equals(invoice.PaymentStatus, "Prepayment", StringComparison.OrdinalIgnoreCase)
                    ? "Waiting For Prepayment"
                    : "Running";
                invoice.PaidDate = null;
                await _db.SaveChangesAsync();
            }

            return Ok(new { paymentRemoved = true, removedAmount = payment.Amount, totalPaid = remainingPaid });
        }

        return ok ? Ok(new { paymentRemoved = false }) : NotFound();
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
