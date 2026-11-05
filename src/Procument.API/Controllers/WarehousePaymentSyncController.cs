using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Procument.API.Services;
using Procument.Data;
using Procument.Module.Catalog.Entities;
using Procument.Module.Identity.Entities;
using Procument.Module.Purchasing.Entities;
using Procument.Module.Sales.Entities;
using Procument.Module.Sales.Services;
using Procument.Shared.Services;

namespace Procument.API.Controllers;

/// <summary>
/// Payment half of the warehouse-app sync (the Payment page's "Sync warehouse" button). Same browser
/// relay as WarehouseSyncController:
///   prepare (payments saved in Ready To Pay, no POP yet) → warehouse exchange → import its paid
///   results (POP + final amount, kept on the PR for review) → ack back;
///   then docs: the supplier files the warehouse is missing, in size-capped batches.
/// Accepting a warehouse result is SupplierPaymentsController.WarehouseAccept. Shipping costs entered on
/// the phone travel the same way and are reviewed in ShippingCostsController; decisions go back on prepare.
/// So do Customer POPs: pushed here with their file, confirmed on the phone (bank POP, price, rate) and
/// accepted in IncomingMoneyController. Wallet transfers and withdraws too: the phone only uploads the POP (the rate
/// of that moment is saved with it), and they wait in Review on the Wallets page. Every new POP (deposit, transfer,
/// withdraw) is queued for the local POP reader (PopReaderWorker), whose numbers the office checks before Accept.
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
    // Decisions are resent until then, so a phone that missed a sync still gets them.
    private const int CostDecisionDays = 90;

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
    private readonly IWalletTransferService _transfers;
    private readonly IWalletWithdrawService _withdraws;
    private readonly PopReaderSignal _reader;

    public WarehousePaymentSyncController(AppDbContext db, IConfiguration config, ICryptoService crypto, IDocumentStorageService storage,
        IWalletTransferService transfers, IWalletWithdrawService withdraws, PopReaderSignal reader)
    {
        _db = db;
        _config = config;
        _crypto = crypto;
        _storage = storage;
        _transfers = transfers;
        _withdraws = withdraws;
        _reader = reader;
    }

    /// <summary>The latest Bank of China USD rate the warehouse read, saved on the last Payment sync (Wallets page).</summary>
    [HttpGet("rate")]
    public async Task<IActionResult> GetRate()
    {
        var s = await _db.Set<WalletSettings>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == WalletSettings.SingletonId);
        return Ok(s?.WarehouseCnyPerUsd is not { } rate ? null : new
        {
            CnyPerUsd = rate,
            WithMarkup = rate + RateMarkup,
            Markup = RateMarkup,
            PublishedAt = s.WarehouseRatePublishedAt,
            FetchedAt = s.WarehouseRateFetchedAt,
            Stale = s.WarehouseRateStale,
            SyncedAt = s.WarehouseRateSyncedAt,
        });
    }

    /// <summary>Same +0.2 the warehouse adds to the BOC rate it saves with a POP (warehouse AutoRate.Markup).</summary>
    public const decimal RateMarkup = 0.2m;

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
            .Select(w => new { w.Id, w.Name, w.Surname, w.Currency, w.HandlingFee, w.WireFee, Company = w.CompanyPreset.Name })
            .ToDictionaryAsync(w => w.Id);
        var invoiceIds = rows.Where(r => r.InvoiceId != null).Select(r => r.InvoiceId!.Value).Distinct().ToList();
        var invoices = await _db.Set<Invoice>().AsNoTracking().Where(i => invoiceIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.InvoiceNumber);

        var push = new PaymentPush { SentAt = DateTime.UtcNow };
        foreach (var r in rows)
        {
            var wallet = wallets.GetValueOrDefault(r.WalletId);
            var (walletName, payingFrom) = PaymentBox.ForWarehouse(wallet?.Name ?? "?", wallet?.Surname, wallet?.Company);
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
                WalletName = walletName,
                PayingFrom = payingFrom,
                WalletCurrency = wallet?.Currency ?? "USD",
                OfficeRate = r.PendingExchangeRate,
                WalletHandlingFee = wallet?.HandlingFee,
                WalletWireFee = wallet?.WireFee,
                OfficeHasResult = r.WarehousePaidAt != null,
                RejectNote = r.WarehouseRejectNote,
                Docs = ListDocs(r.Origin == "Stock", salesOrder ?? r.PONumber, r.SupplierName, r.PONumber)
                    .Select(d => new PaymentDocInfo { Key = d.Key, Kind = d.Kind, Name = d.Name, Size = d.Size })
                    .ToList(),
            });
        }

        var decidedSince = DateTime.UtcNow.AddDays(-CostDecisionDays);
        push.CostDecisions = await _db.Set<WarehouseShippingCost>().AsNoTracking()
            .Where(c => c.Status != WarehouseShippingCost.Pending && c.ReviewedAt >= decidedSince)
            .Select(c => new ShippingCostDecision
            {
                ShipmentId = c.WarehouseShipmentId, Version = c.Version, Status = c.Status, Note = c.RejectNote,
                FixedCost = c.FixedCost, OtherCost = c.OtherCost,
            })
            .ToListAsync();

        var deposits = await (
            from d in OpenDeposits()
            join c in _db.Set<Customer>() on d.CustomerPayment.Invoice.CustomerId equals c.Id into customers
            from c in customers.DefaultIfEmpty()
            select new
            {
                d.Id, d.UploadId, d.CustomerPayment.Invoice.InvoiceNumber, Customer = c != null ? c.CustomerCode : null,
                d.Amount, d.Currency, d.WalletRate, d.OfficeAmount, d.BankAt, d.RejectNote,
                WalletName = d.PaymentBox.Name, d.PaymentBox.Surname, WalletCurrency = d.PaymentBox.Currency, Company = d.PaymentBox.CompanyPreset.Name,
                PopFileName = d.CustomerPayment.FileName, d.CustomerPayment.Notes,
            })
            .ToListAsync();
        foreach (var d in deposits)
        {
            var pop = CustomerPopDoc(d.InvoiceNumber, d.PopFileName);
            var (walletName, company) = PaymentBox.ForWarehouse(d.WalletName, d.Surname, d.Company);
            push.Deposits.Add(new DepositPushItem
            {
                UploadId = d.UploadId,
                MainDepositId = d.Id,
                InvoiceNumber = d.InvoiceNumber,
                Customer = d.Customer,
                Amount = d.Amount,
                Currency = d.Currency,
                WalletName = walletName,
                CompanyName = company,
                WalletCurrency = d.WalletCurrency,
                OfficeRate = d.WalletRate,
                OfficeAmount = d.OfficeAmount,
                Notes = d.Notes,
                OfficeHasResult = d.BankAt != null,
                RejectNote = d.RejectNote,
                Pop = pop == null ? null : new PaymentDocInfo { Key = pop.Key, Kind = pop.Kind, Name = pop.Name, Size = pop.Size },
            });
        }

        var transfers = await (
            from t in _db.Set<WalletTransferPending>().AsNoTracking()
            where t.Status == "Accepted"
            join u in _db.Set<User>() on t.CreatedByUserId equals u.Id into users
            from u in users.DefaultIfEmpty()
            orderby t.CreatedAt
            select new
            {
                t.Id, t.WithdrawAmount, t.DepositAmount, t.ExchangeRate, t.RateBaseCurrency, t.Notes, t.CreatedAt,
                CreatedBy = u != null ? u.Name : null,
                FromName = t.FromBox.Name, FromSurname = t.FromBox.Surname, FromCompany = t.FromBox.CompanyPreset.Name,
                FromCurrency = t.FromBox.Currency, FromHandlingFee = t.FromBox.HandlingFee,
                ToName = t.ToBox.Name, ToSurname = t.ToBox.Surname, ToCompany = t.ToBox.CompanyPreset.Name,
                ToCurrency = t.ToBox.Currency, ToWireFee = t.ToBox.WireFee,
            })
            .ToListAsync();
        foreach (var t in transfers)
        {
            var (fromWallet, fromCompany) = PaymentBox.ForWarehouse(t.FromName, t.FromSurname, t.FromCompany);
            var (toWallet, toCompany) = PaymentBox.ForWarehouse(t.ToName, t.ToSurname, t.ToCompany);
            push.Transfers.Add(new TransferPushItem
            {
                MainTransferId = t.Id,
                FromWallet = fromWallet,
                FromCompany = fromCompany,
                FromCurrency = t.FromCurrency,
                ToWallet = toWallet,
                ToCompany = toCompany,
                ToCurrency = t.ToCurrency,
                Amount = t.WithdrawAmount,
                // Older transfers kept "1 From = rate To": the phone only understands the new form, so not those.
                OfficeRate = t.RateBaseCurrency != null ? t.ExchangeRate : null,
                OfficeReceived = t.DepositAmount,
                FromHandlingFee = t.FromHandlingFee,
                ToWireFee = t.ToWireFee,
                Notes = t.Notes,
                CreatedBy = t.CreatedBy,
                CreatedAt = t.CreatedAt,
            });
        }

        var withdraws = await (
            from w in _db.Set<WalletWithdrawPending>().AsNoTracking()
            where w.Status == WalletWithdrawPending.Pending
            join u in _db.Set<User>() on w.CreatedByUserId equals u.Id into users
            from u in users.DefaultIfEmpty()
            join pr in _db.Set<PaymentRequest>() on w.PaymentRequestId equals pr.Id into prs
            from pr in prs.DefaultIfEmpty()
            orderby w.CreatedAt
            select new
            {
                w.Id, w.Amount, w.Currency, w.OfficeRate, w.ToType, w.Notes, w.CreatedAt,
                Supplier = w.ToSupplier != null ? w.ToSupplier.Name : null, CreatedBy = u != null ? u.Name : null,
                PrNumber = pr != null ? pr.PRId : null,
                WalletName = w.PaymentBox.Name, w.PaymentBox.Surname, Company = w.PaymentBox.CompanyPreset.Name,
                WalletCurrency = w.PaymentBox.Currency, w.PaymentBox.HandlingFee, w.PaymentBox.WireFee,
            })
            .ToListAsync();
        foreach (var w in withdraws)
        {
            var (walletName, company) = PaymentBox.ForWarehouse(w.WalletName, w.Surname, w.Company);
            push.Withdraws.Add(new WithdrawPushItem
            {
                MainWithdrawId = w.Id,
                WalletName = walletName,
                CompanyName = company,
                WalletCurrency = w.WalletCurrency,
                Amount = w.Amount,
                Currency = w.Currency ?? w.WalletCurrency,
                OfficeRate = w.OfficeRate,
                WalletHandlingFee = w.HandlingFee,
                WalletWireFee = w.WireFee,
                ToName = w.ToType == "Supplier" ? w.Supplier ?? "Supplier" : "Mother Wallet",
                PrNumber = w.PrNumber,
                Notes = w.Notes,
                CreatedBy = w.CreatedBy,
                CreatedAt = w.CreatedAt,
            });
        }

        var cipher = _crypto.EncryptBrowser(JsonSerializer.Serialize(push, Json), SharedKey, out var iv);
        return Ok(new
        {
            CipherText = cipher, Iv = iv, PaymentCount = push.Payments.Count, DepositCount = push.Deposits.Count,
            TransferCount = push.Transfers.Count, WithdrawCount = push.Withdraws.Count,
        });
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
            foreach (var c in results.Costs)
            {
                var reason = await ImportCostAsync(c);
                ack.Costs.Add(new ShippingCostAck { ShipmentId = c.ShipmentId, Version = c.Version, Stored = reason == null, Reason = reason });
                if (reason != null) skipped.Add(reason);
            }
            foreach (var d in results.Deposits)
            {
                var reason = await ImportDepositAsync(d);
                ack.Deposits.Add(new DepositResultAck { DepositId = d.DepositId, Version = d.Version, Stored = reason == null, Reason = reason });
                if (reason != null) skipped.Add(reason);
            }
            foreach (var t in results.Transfers)
            {
                var reason = await ImportTransferAsync(t);
                ack.Transfers.Add(new TransferResultAck { TransferId = t.TransferId, Version = t.Version, Stored = reason == null, Reason = reason });
                if (reason != null) skipped.Add(reason);
            }
            foreach (var w in results.Withdraws)
            {
                var reason = await ImportWithdrawAsync(w);
                ack.Withdraws.Add(new WithdrawResultAck { WithdrawId = w.WithdrawId, Version = w.Version, Stored = reason == null, Reason = reason });
                if (reason != null) skipped.Add(reason);
            }
            await SaveLatestRateAsync(results.LatestRate);
        }
        finally
        {
            ImportLock.Release();
        }
        // New POPs are queued: let the reader start now rather than on its next round.
        if (ack.Payments.Any(a => a.Stored) || ack.Deposits.Any(a => a.Stored) || ack.Transfers.Any(a => a.Stored)
            || ack.Withdraws.Any(a => a.Stored)) _reader.Poke();

        var cipher = _crypto.EncryptBrowser(JsonSerializer.Serialize(ack, Json), SharedKey, out var iv);
        return Ok(new
        {
            Ack = new WarehouseSyncEnvelope { CipherText = cipher, Iv = iv },
            Summary = new
            {
                Stored = ack.Payments.Count(a => a.Stored), Costs = ack.Costs.Count(a => a.Stored),
                Deposits = ack.Deposits.Count(a => a.Stored), Transfers = ack.Transfers.Count(a => a.Stored),
                Withdraws = ack.Withdraws.Count(a => a.Stored), Skipped = skipped,
            },
        });
    }

    /// <summary>Stores the POP and the rate; the withdraw waits in Review for the office. Null = stored; otherwise why not (the phone shows it).</summary>
    private async Task<string?> ImportWithdrawAsync(WithdrawResult r)
    {
        _db.ChangeTracker.Clear();
        byte[] bytes;
        try { bytes = Convert.FromBase64String(r.DataBase64); }
        catch (FormatException) { return $"Withdraw #{r.MainWithdrawId}: the POP file was damaged on the way."; }
        if (bytes.Length == 0) return $"Withdraw #{r.MainWithdrawId}: the POP file is empty.";
        return await _withdraws.StoreFromWarehouseAsync(r.MainWithdrawId, r.ExchangeRate, r.BocRate, r.RateAt,
            r.By ?? "", r.At, Path.GetFileName(r.FileName), bytes, WalletTransferService.StorageRoot(_config));
    }

    /// <summary>Stores the POP and the rate; the transfer waits in Review for the office. Null = stored; otherwise why not (the phone shows it).</summary>
    private async Task<string?> ImportTransferAsync(TransferResult r)
    {
        _db.ChangeTracker.Clear();
        byte[] bytes;
        try { bytes = Convert.FromBase64String(r.DataBase64); }
        catch (FormatException) { return $"Transfer #{r.MainTransferId}: the POP file was damaged on the way."; }
        if (bytes.Length == 0) return $"Transfer #{r.MainTransferId}: the POP file is empty.";
        return await _transfers.StoreFromWarehouseAsync(r.MainTransferId, r.ExchangeRate, r.BocRate, r.RateAt,
            r.By ?? "", r.At, Path.GetFileName(r.FileName), bytes, WalletTransferService.StorageRoot(_config));
    }

    /// <summary>Saves the latest Bank of China rate the warehouse read (shown on Wallets).</summary>
    private async Task SaveLatestRateAsync(LatestRateInfo? rate)
    {
        if (rate is not { CnyPerUsd: > 0 }) return;
        _db.ChangeTracker.Clear();
        var s = await _db.Set<WalletSettings>().FirstOrDefaultAsync(x => x.Id == WalletSettings.SingletonId);
        if (s == null) _db.Set<WalletSettings>().Add(s = new WalletSettings());
        s.WarehouseCnyPerUsd = Math.Round(rate.CnyPerUsd, 6);
        s.WarehouseRatePublishedAt = rate.PublishedAt?.Length > 50 ? rate.PublishedAt[..50] : rate.PublishedAt;
        s.WarehouseRateFetchedAt = rate.FetchedAt;
        s.WarehouseRateStale = rate.Stale;
        s.WarehouseRateSyncedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    /// <summary>Null = stored; otherwise why not (the phone shows it and the user can fix and resend).</summary>
    private async Task<string?> ImportDepositAsync(DepositResult r)
    {
        _db.ChangeTracker.Clear();
        var row = await _db.Set<IncomingDeposit>()
            .Include(d => d.CustomerPayment).ThenInclude(p => p.Invoice)
            .Include(d => d.PaymentBox)
            .FirstOrDefaultAsync(d => d.UploadId == r.UploadId);
        if (row == null) return "This customer POP was removed in the office.";
        var invoiceNumber = row.CustomerPayment.Invoice.InvoiceNumber;
        if (row.Status != IncomingDeposit.Pending) return $"{invoiceNumber}: this deposit was already accepted.";

        // The phone no longer types a price: Amount is the customer POP's for now, the POP reader reads the real one.
        var amount = r.Amount > 0 && decimal.Round(r.Amount, 2) == r.Amount ? r.Amount : row.Amount;
        var walletCurrency = row.PaymentBox.Currency;
        var sameCurrency = string.Equals(row.Currency, walletCurrency, StringComparison.OrdinalIgnoreCase);
        var rate = sameCurrency ? null
            : r.ExchangeRate is > 0 && decimal.Round(r.ExchangeRate.Value, 6) == r.ExchangeRate.Value ? r.ExchangeRate
            : row.WalletRate;

        byte[] bytes;
        try { bytes = Convert.FromBase64String(r.DataBase64); }
        catch (FormatException) { return $"{invoiceNumber}: the bank POP file was damaged on the way."; }
        if (bytes.Length == 0) return $"{invoiceNumber}: the bank POP file is empty.";

        // A resend (edited on the phone after it was stored) replaces the earlier POP.
        if (row.BankPopFileName != null)
            _storage.DeleteFileInInvoiceCategory(invoiceNumber, IncomingDeposit.BankPopFolder, row.BankPopFileName);
        using (var stream = new MemoryStream(bytes))
        {
            row.BankPopFileName = _storage.SaveFileInInvoiceCategory(invoiceNumber, IncomingDeposit.BankPopFolder,
                $"{r.UploadId:N}-{Path.GetFileName(r.FileName)}", stream);
        }
        row.BankAmount = amount;
        row.BankRate = rate;
        row.BocRate = sameCurrency ? null : r.BocRate;
        row.RateAt = sameCurrency ? null : r.RateAt?.Length > 50 ? r.RateAt[..50] : r.RateAt;
        // Worked out here, like the Procument side, so both are rounded the same way.
        row.BankWalletAmount = IncomingDeposit.WalletAmount(amount, rate);
        row.BankBy = string.IsNullOrWhiteSpace(r.By) ? "Warehouse" : r.By.Trim()[..Math.Min(200, r.By.Trim().Length)];
        row.BankAt = r.At == default ? DateTime.UtcNow : r.At;
        row.RejectNote = null;
        await PopReading.QueueAsync(_db, PopReading.Deposit, row.Id);
        await _db.SaveChangesAsync();
        return null;
    }

    /// <summary>Null = stored; otherwise why not (the phone shows it).</summary>
    private async Task<string?> ImportCostAsync(ShippingCostChange c)
    {
        _db.ChangeTracker.Clear();
        var row = await _db.Set<WarehouseShippingCost>().FirstOrDefaultAsync(x => x.WarehouseShipmentId == c.ShipmentId);
        if (row?.Status == WarehouseShippingCost.Accepted) return $"{c.TrackNumber}: the shipping cost was already accepted.";
        if (row != null && row.Version >= c.Version) return null;   // already here, ack lost on the way back
        if (c.FixedCost < 0 || c.OtherCost < 0 || decimal.Round(c.FixedCost, 2) != c.FixedCost || decimal.Round(c.OtherCost, 2) != c.OtherCost)
            return $"{c.TrackNumber}: the shipping cost must be 0 or more with at most two decimals.";

        // Cleared on the phone: nothing to pay, nothing to review.
        if (c.FixedCost + c.OtherCost == 0)
        {
            if (row != null) _db.Set<WarehouseShippingCost>().Remove(row);
            await _db.SaveChangesAsync();
            return null;
        }

        if (row == null) _db.Set<WarehouseShippingCost>().Add(row = new WarehouseShippingCost { WarehouseShipmentId = c.ShipmentId });
        row.Version = c.Version;
        row.TrackNumber = c.TrackNumber;
        row.Carrier = c.Carrier;
        row.WarehouseName = c.WarehouseName;
        row.TrackIds = string.Join(",", c.MainTrackIds);
        row.FixedCost = c.FixedCost;
        row.OtherCost = c.OtherCost;
        row.EnteredBy = string.IsNullOrWhiteSpace(c.By) ? "Warehouse" : c.By.Trim()[..Math.Min(200, c.By.Trim().Length)];
        row.EnteredAt = c.At == default ? DateTime.UtcNow : c.At;
        row.Status = WarehouseShippingCost.Pending;
        row.RejectNote = null;
        row.ReviewedAt = null;
        row.ReviewedByUserId = null;
        await _db.SaveChangesAsync();
        return null;
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
        // Saved with the POP on the phone (BOC + 0.2 for yuan); the office's own rate when it came without one.
        var rate = isUsd ? 1m
            : r.ExchangeRate is > 0 && decimal.Round(r.ExchangeRate.Value, 6) == r.ExchangeRate.Value ? r.ExchangeRate
            : pr.PendingExchangeRate;
        if (rate is null or <= 0)
            return $"{label}: there is no {currency} per 1 USD rate for this payment.";
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
        pr.WarehouseBocRate = isUsd || rate != r.ExchangeRate ? null : r.BocRate;
        pr.WarehouseRateAt = pr.WarehouseBocRate == null ? null : r.RateAt?.Length > 50 ? r.RateAt[..50] : r.RateAt;
        pr.WarehousePaidBy = string.IsNullOrWhiteSpace(r.PaidBy) ? "Warehouse" : r.PaidBy.Trim()[..Math.Min(200, r.PaidBy.Trim().Length)];
        pr.WarehousePaidAt = r.PaidAt == default ? DateTime.UtcNow : r.PaidAt;
        pr.WarehouseRejectNote = null;
        // The local AI reads the price and the bank fees off the POP for the office's review.
        await PopReading.QueueAsync(_db, PopReading.Payment, pr.Id);
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
            Dictionary<string, DocFile> docs;
            Func<DocFile, (Stream Stream, string AbsolutePath)?> open;
            var deposit = await OpenDeposits()
                .Where(d => d.UploadId == group.Key)
                .Select(d => new { d.CustomerPayment.Invoice.InvoiceNumber, d.CustomerPayment.FileName })
                .FirstOrDefaultAsync();
            if (deposit != null)
            {
                var pop = CustomerPopDoc(deposit.InvoiceNumber, deposit.FileName);
                docs = pop == null ? new() : new() { [pop.Key] = pop };
                open = d => _storage.OpenFileInInvoiceCategory(deposit.InvoiceNumber, d.Folder, d.Name);
            }
            else
            {
                var payment = await OpenPayments()
                    .Where(p => p.PendingUploadId == group.Key)
                    .Select(p => new { p.PO!.PONumber, p.PO.Origin, p.PO.InvoiceId, SupplierName = p.PO.Supplier.Name })
                    .FirstOrDefaultAsync();
                if (payment == null) continue;   // finished meanwhile: the warehouse closes it on the next sync
                var salesOrder = payment.InvoiceId == null ? null
                    : await _db.Set<Invoice>().Where(i => i.Id == payment.InvoiceId).Select(i => i.InvoiceNumber).FirstOrDefaultAsync();
                var root = salesOrder ?? payment.PONumber;
                docs = ListDocs(payment.Origin == "Stock", root, payment.SupplierName, payment.PONumber).ToDictionary(d => d.Key);
                open = d => _storage.OpenFileInSupplierCategory(root, payment.SupplierName, d.Folder, d.Name);
            }

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
                    var file = open(doc);
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

    /// <summary>Customer POPs not accepted yet (still on the phone, or waiting in Wallets).</summary>
    private IQueryable<IncomingDeposit> OpenDeposits() => _db.Set<IncomingDeposit>().AsNoTracking()
        .Where(d => d.Status == IncomingDeposit.Pending);

    private record DocFile(string Key, string Kind, string Folder, string Name, long Size);

    private DocFile? CustomerPopDoc(string invoiceNumber, string fileName)
    {
        var file = _storage.ListFilesInInvoiceCategories(invoiceNumber, [("customer_pop", IncomingDeposit.CustomerPopFolder)])
            .FirstOrDefault(f => f.Name == fileName);
        return file == null ? null
            : new DocFile($"customer_pop/{file.Name}/{file.ModifiedAt.Ticks}", "Customer POP", IncomingDeposit.CustomerPopFolder, file.Name, file.Size);
    }

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
