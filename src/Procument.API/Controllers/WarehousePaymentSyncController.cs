using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Procument.API.Services;
using Procument.Data;
using Procument.Module.Purchasing.Entities;
using Procument.Module.Sales.Entities;
using Procument.Shared.Services;

namespace Procument.API.Controllers;

/// <summary>
/// Payment half of the warehouse-app sync (the Payment page's "Sync warehouse" button). Same browser
/// relay as WarehouseSyncController:
///   prepare (payments saved in Ready To Pay, no POP yet) → warehouse exchange → import its paid
///   results (POP + final amount, kept on the PR for review) → ack back;
///   then docs: the supplier files the warehouse is missing, in size-capped batches.
/// Accepting a warehouse result is SupplierPaymentsController.WarehouseAccept.
/// </summary>
[Authorize(Roles = "Payment,AHM,Admin,SuperAdmin")]
[ApiController]
[Route("api/warehouse-sync/payments")]
public class WarehousePaymentSyncController : ControllerBase
{
    private const string PopFolder = "Our POP to Supplier";
    // Same budget as the warehouse batches: files are base64'd inside a base64'd envelope.
    private const long DocBudgetBytes = 12 * 1024 * 1024;
    private const int DocBatchMax = 40;
    private const long DocMaxBytes = 15 * 1024 * 1024;

    /// <summary>Supplier folders shown on the phone (Key, folder, label), as on the Payment page.</summary>
    private static readonly (string Key, string Folder, string Kind)[] CustomerPoDocs =
    {
        ("supplier_invoice", "Supplier Invoice", "Supplier invoice"),
        ("supplier_bank_info", "Supplier Bank Info", "Bank info"),
        ("po", "PO", "PO PDF"),
        ("dp", "DP", "Payment request"),
    };
    private static readonly (string Key, string Folder, string Kind)[] StockPoDocs =
    {
        ("supplier_pi", "Supplier PI", "Supplier PI"),
        ("supplier_invoice", "Supplier Invoice", "Supplier invoice"),
        ("po", "PO", "PO PDF"),
        ("dp", "DP", "Payment request"),
    };

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly SemaphoreSlim ImportLock = new(1, 1);

    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly ICryptoService _crypto;
    private readonly IDocumentStorageService _storage;

    public WarehousePaymentSyncController(AppDbContext db, IConfiguration config, ICryptoService crypto, IDocumentStorageService storage)
    {
        _db = db;
        _config = config;
        _crypto = crypto;
        _storage = storage;
    }

    private string SharedKey => _config["WarehouseSync:SharedKey"] is { Length: > 0 } k && k != "REPLACEME"
        ? k
        : throw new InvalidOperationException("WarehouseSync:SharedKey not configured");

    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        var url = _config["WarehouseSync:Url"]?.TrimEnd('/') ?? string.Empty;
        return Ok(new { Enabled = url.Length > 0, WarehouseUrl = url, ApiKey = _config["WarehouseSync:ApiKey"] ?? string.Empty });
    }

    [HttpGet("prepare")]
    public async Task<IActionResult> Prepare()
    {
        var rows = await OpenPayments()
            .Select(p => new
            {
                p.Id, p.PRId, UploadId = p.PendingUploadId!.Value, Amount = p.PendingPaymentAmount!.Value,
                p.PendingExchangeRate, WalletId = p.PendingWalletId!.Value, PrAmount = p.Amount,
                p.WarehousePaidAt, p.WarehouseRejectNote,
                PoId = p.POId!.Value, p.PO!.PONumber, p.PO.Origin, p.PO.InvoiceId,
                SupplierName = p.PO.Supplier.Name, p.PO.Supplier.Email, p.PO.Supplier.Phone, p.PO.Supplier.Address, p.PO.Supplier.Contacts,
                Bank = p.PO.ImportDetail,
            })
            .ToListAsync();

        var walletIds = rows.Select(r => r.WalletId).Distinct().ToList();
        var wallets = await _db.Set<PaymentBox>().AsNoTracking().Where(w => walletIds.Contains(w.Id))
            .Select(w => new { w.Id, w.Name, w.Currency, Company = w.CompanyPreset.Name })
            .ToDictionaryAsync(w => w.Id);
        var invoiceIds = rows.Where(r => r.InvoiceId != null).Select(r => r.InvoiceId!.Value).Distinct().ToList();
        var invoices = await _db.Set<Invoice>().AsNoTracking().Where(i => invoiceIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.InvoiceNumber);

        var push = new PaymentPush { SentAt = DateTime.UtcNow };
        foreach (var r in rows)
        {
            var wallet = wallets.GetValueOrDefault(r.WalletId);
            var salesOrder = r.InvoiceId is { } invoiceId ? invoices.GetValueOrDefault(invoiceId) : null;
            push.Payments.Add(new PaymentPushItem
            {
                UploadId = r.UploadId,
                MainPaymentRequestId = r.Id,
                PoId = r.PoId,
                PrNumber = r.PRId,
                PoNumber = r.PONumber,
                PoType = r.Origin == "Stock" ? "Stock PO" : "Customer PO",
                SalesOrder = salesOrder,
                SupplierName = r.SupplierName,
                SupplierEmail = r.Email,
                SupplierPhone = r.Phone,
                SupplierAddress = r.Address,
                SupplierContacts = ParseContacts(r.Contacts),
                Beneficiary = r.Bank?.Beneficiary,
                Reference = r.Bank?.Reference,
                BankName = r.Bank?.BankName,
                BankAccountNumber = r.Bank?.BankAccountNumber,
                BankAddress = r.Bank?.BankAddress,
                BankCity = r.Bank?.BankCity,
                BankCountry = r.Bank?.BankCountry,
                SwiftCode = r.Bank?.SwiftCode,
                Aba = r.Bank?.ABA,
                AmountUsd = r.Amount,
                WireFee = r.Bank?.Wirefee ?? 0,
                PrAmount = r.PrAmount,
                WalletName = wallet?.Name ?? "?",
                PayingFrom = wallet?.Company,
                WalletCurrency = wallet?.Currency ?? "USD",
                OfficeRate = r.PendingExchangeRate,
                OfficeHasResult = r.WarehousePaidAt != null,
                RejectNote = r.WarehouseRejectNote,
                Docs = ListDocs(r.Origin == "Stock", salesOrder ?? r.PONumber, r.SupplierName, r.PONumber)
                    .Select(d => new PaymentDocInfo { Key = d.Key, Kind = d.Kind, Name = d.Name, Size = d.Size })
                    .ToList(),
            });
        }

        var cipher = _crypto.EncryptBrowser(JsonSerializer.Serialize(push, Json), SharedKey, out var iv);
        return Ok(new { CipherText = cipher, Iv = iv, PaymentCount = push.Payments.Count });
    }

    /// <summary>Store paid results from the warehouse; each one waits on its PR for review.</summary>
    [HttpPost("import")]
    [RequestSizeLimit(100_000_000)]
    public async Task<IActionResult> Import([FromBody] WarehouseSyncEnvelope envelope)
    {
        var results = Open<PaymentResults>(envelope);
        if (results == null)
            return BadRequest(new { message = "Could not decrypt the warehouse data — check WarehouseSync:SharedKey on both servers." });

        var ack = new PaymentAck();
        var skipped = new List<string>();
        await ImportLock.WaitAsync();
        try
        {
            foreach (var r in results.Payments)
            {
                var reason = await ImportOneAsync(r);
                ack.Payments.Add(new PaymentResultAck { PaymentId = r.PaymentId, Version = r.Version, Stored = reason == null, Reason = reason });
                if (reason != null) skipped.Add(reason);
            }
        }
        finally
        {
            ImportLock.Release();
        }

        var cipher = _crypto.EncryptBrowser(JsonSerializer.Serialize(ack, Json), SharedKey, out var iv);
        return Ok(new
        {
            Ack = new WarehouseSyncEnvelope { CipherText = cipher, Iv = iv },
            Summary = new { Stored = ack.Payments.Count(a => a.Stored), Skipped = skipped },
        });
    }

    /// <summary>Null = stored; otherwise why not (the phone shows it and the user can fix and resend).</summary>
    private async Task<string?> ImportOneAsync(PaymentResult r)
    {
        _db.ChangeTracker.Clear();
        var pr = await _db.Set<PaymentRequest>()
            .Include(p => p.PO!).ThenInclude(po => po.Supplier)
            .FirstOrDefaultAsync(p => p.PendingUploadId == r.UploadId && p.POId != null);
        if (pr?.PO == null || pr.PendingPaymentAmount == null || pr.PendingWalletId == null)
            return "This payment was changed or finished in the office.";
        var label = $"PR-{pr.PRId} / {pr.PO.PONumber}";
        if (await _db.Set<PaymentTransaction>().AnyAsync(t => t.PopUploadId == r.UploadId))
            return $"{label}: already paid in the office.";

        var currency = await _db.Set<PaymentBox>().Where(w => w.Id == pr.PendingWalletId).Select(w => w.Currency).FirstOrDefaultAsync();
        if (currency == null) return $"{label}: the office wallet no longer exists.";
        var isUsd = string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase);
        var rate = isUsd ? 1m : r.ExchangeRate;
        if (rate is null or <= 0 || decimal.Round(rate.Value, 6) != rate.Value)
            return $"{label}: enter the rate as {currency} per 1 USD (up to six decimals).";
        if (r.HandlingFee < 0 || r.BankWireFee < 0
            || decimal.Round(r.HandlingFee, 2) != r.HandlingFee || decimal.Round(r.BankWireFee, 2) != r.BankWireFee)
            return $"{label}: the handling and wire fees must be 0 or more with at most two decimals.";
        // Worked out here from the current saved amount, so an office change can't leave a stale total.
        var debit = Math.Round(pr.PendingPaymentAmount.Value * rate.Value, 2, MidpointRounding.AwayFromZero);
        var finalAmount = debit + r.HandlingFee + r.BankWireFee;

        byte[] bytes;
        try { bytes = Convert.FromBase64String(r.DataBase64); }
        catch (FormatException) { return $"{label}: the POP file was damaged on the way."; }
        if (bytes.Length == 0) return $"{label}: the POP file is empty.";

        var folder = await _db.Set<Invoice>().Where(i => i.Id == pr.PO.InvoiceId).Select(i => i.InvoiceNumber).FirstOrDefaultAsync()
            ?? pr.PO.PONumber;
        // A resend (edited on the phone after it was stored) replaces the earlier POP.
        if (pr.WarehousePopFileName != null)
            _storage.DeleteFileInSupplierCategory(folder, pr.PO.Supplier.Name, PopFolder, pr.WarehousePopFileName);
        using (var stream = new MemoryStream(bytes))
        {
            pr.WarehousePopFileName = _storage.SaveFileInSupplierCategory(folder, pr.PO.Supplier.Name, PopFolder,
                $"PR-{pr.PRId}-{r.UploadId:N}-{Path.GetFileName(r.FileName)}", stream);
        }
        pr.WarehouseHandlingFee = r.HandlingFee;
        pr.WarehouseBankWireFee = r.BankWireFee;
        pr.WarehouseFinalAmount = finalAmount;
        pr.WarehouseExchangeRate = rate;
        pr.WarehousePaidBy = string.IsNullOrWhiteSpace(r.PaidBy) ? "Warehouse" : r.PaidBy.Trim()[..Math.Min(200, r.PaidBy.Trim().Length)];
        pr.WarehousePaidAt = r.PaidAt == default ? DateTime.UtcNow : r.PaidAt;
        pr.WarehouseRejectNote = null;
        await _db.SaveChangesAsync();
        return null;
    }

    /// <summary>
    /// The supplier files the warehouse asked for (its encrypted list from the exchange), as one
    /// size-capped batch to relay, plus the rest re-encrypted for the next call.
    /// </summary>
    [HttpPost("docs")]
    public async Task<IActionResult> Docs([FromBody] WarehouseSyncEnvelope envelope)
    {
        var request = Open<PaymentDocRequest>(envelope);
        if (request == null) return BadRequest(new { message = "Could not decrypt the warehouse document list." });

        var batch = new PaymentDocBatch();
        var remaining = new PaymentDocRequest();
        long budget = 0;
        foreach (var group in request.Items.GroupBy(i => i.UploadId))
        {
            var payment = await OpenPayments()
                .Where(p => p.PendingUploadId == group.Key)
                .Select(p => new { p.PO!.PONumber, p.PO.Origin, p.PO.InvoiceId, SupplierName = p.PO.Supplier.Name })
                .FirstOrDefaultAsync();
            if (payment == null) continue;   // finished meanwhile: the warehouse closes it on the next sync
            var salesOrder = payment.InvoiceId == null ? null
                : await _db.Set<Invoice>().Where(i => i.Id == payment.InvoiceId).Select(i => i.InvoiceNumber).FirstOrDefaultAsync();
            var root = salesOrder ?? payment.PONumber;
            var docs = ListDocs(payment.Origin == "Stock", root, payment.SupplierName, payment.PONumber).ToDictionary(d => d.Key);

            foreach (var item in group)
            {
                if (!docs.TryGetValue(item.Key, out var doc)) continue;   // gone; dropped from the next push
                var full = batch.Docs.Count >= DocBatchMax || (batch.Docs.Count > 0 && budget + doc.Size > DocBudgetBytes);
                if (full)
                {
                    remaining.Items.Add(item);
                    continue;
                }
                var data = new PaymentDocData { UploadId = group.Key, Key = doc.Key, Kind = doc.Kind, Name = doc.Name, MimeType = MimeOf(doc.Name) };
                if (doc.Size > DocMaxBytes)
                {
                    data.TooLarge = true;
                }
                else
                {
                    var file = _storage.OpenFileInSupplierCategory(root, payment.SupplierName, doc.Folder, doc.Name);
                    if (file == null) continue;
                    await using (file.Value.Stream)
                    using (var ms = new MemoryStream())
                    {
                        await file.Value.Stream.CopyToAsync(ms);
                        data.DataBase64 = Convert.ToBase64String(ms.ToArray());
                    }
                    budget += doc.Size;
                }
                batch.Docs.Add(data);
            }
        }

        var cipher = _crypto.EncryptBrowser(JsonSerializer.Serialize(batch, Json), SharedKey, out var iv);
        WarehouseSyncEnvelope? next = null;
        if (remaining.Items.Count > 0)
        {
            var nextCipher = _crypto.EncryptBrowser(JsonSerializer.Serialize(remaining, Json), SharedKey, out var nextIv);
            next = new WarehouseSyncEnvelope { CipherText = nextCipher, Iv = nextIv };
        }
        return Ok(new { Batch = new WarehouseSyncEnvelope { CipherText = cipher, Iv = iv }, Count = batch.Docs.Count, Next = next, Remaining = remaining.Items.Count });
    }

    // ── Helpers ──

    /// <summary>Saved in Ready To Pay (payment details) and no POP recorded yet.</summary>
    private IQueryable<PaymentRequest> OpenPayments() => _db.Set<PaymentRequest>().AsNoTracking()
        .Where(p => p.POId != null && p.PendingUploadId != null && p.PendingPaymentAmount != null && p.PendingWalletId != null
            && p.PO!.PaymentApproval == "Accepted" && p.PO.Status != "Cancelled" && p.PO.Status != "Returned"
            && !_db.Set<PaymentTransaction>().Any(t => t.PopUploadId == p.PendingUploadId));

    private record DocFile(string Key, string Kind, string Folder, string Name, long Size);

    /// <summary>Key = folder key + name + modified time, so a replaced file is fetched again.</summary>
    private List<DocFile> ListDocs(bool isStock, string root, string supplierName, string poNumber)
    {
        var categories = isStock ? StockPoDocs : CustomerPoDocs;
        return _storage.ListFilesInSupplierCategories(root, supplierName, categories.Select(c => (c.Key, c.Folder)))
            // A Sales Order's PO folder can hold other POs of the same supplier: keep this PO's.
            .Where(f => isStock || f.Category != "po" || f.Name == $"{poNumber}.pdf" || f.Name.StartsWith($"{poNumber} ("))
            .Select(f =>
            {
                var c = categories.First(x => x.Key == f.Category);
                return new DocFile($"{f.Category}/{f.Name}/{f.ModifiedAt.Ticks}", c.Kind, c.Folder, f.Name, f.Size);
            })
            .OrderBy(f => Array.FindIndex(categories, c => c.Folder == f.Folder))
            .ToList();
    }

    private static List<string> ParseContacts(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return new();
            return doc.RootElement.EnumerateArray()
                .Select(c => string.Join(" · ", new[] { "name", "title", "email", "phone" }
                    .Select(k => c.ValueKind == JsonValueKind.Object && c.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null)
                    .Where(v => !string.IsNullOrWhiteSpace(v))))
                .Where(line => line.Length > 0)
                .ToList();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    private static string MimeOf(string name)
        => new FileExtensionContentTypeProvider().TryGetContentType(name, out var mime) ? mime : "application/octet-stream";

    private T? Open<T>(WarehouseSyncEnvelope envelope) where T : class
    {
        try { return JsonSerializer.Deserialize<T>(_crypto.DecryptBrowser(envelope.CipherText, envelope.Iv, SharedKey), Json); }
        catch (Exception e) when (e is System.Security.Cryptography.CryptographicException or FormatException or JsonException) { return null; }
    }
}
