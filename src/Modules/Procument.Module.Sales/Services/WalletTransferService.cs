using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Procument.Module.Identity.Entities;
using Procument.Module.Sales.DTOs;
using Procument.Module.Sales.Entities;

namespace Procument.Module.Sales.Services;

/// <summary>
/// Wallet-to-wallet transfers: Accepted (waiting for the POP) → Review (POP in, from the warehouse or uploaded here; the
/// local POP reader reads it) → Completed (the office accepted the numbers, PopFees works out the proposal).
/// </summary>
public class WalletTransferService : IWalletTransferService
{
    public const string Accepted = "Accepted";
    public const string Review = "Review";
    public const string Completed = "Completed";

    private readonly DbContext _db;

    public WalletTransferService(DbContext db) => _db = db;

    /// <summary>Folder that holds WalletTransfers/{id}/pop: next to the proforma invoice documents.</summary>
    public static string StorageRoot(IConfiguration config)
    {
        var configured = config["DocumentStorage:ProformaInvoicesRoot"];
        return string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(AppContext.BaseDirectory, "Documents")
            : Path.GetDirectoryName(
                Path.IsPathRooted(configured)
                    ? configured
                    : Path.Combine(AppContext.BaseDirectory, configured))!;
    }

    public static string PopPath(string storageRoot, long id, string fileName)
        => Path.Combine(storageRoot, "WalletTransfers", id.ToString(), "pop", fileName);

    public async Task<List<WalletTransferPendingResponse>> GetAllAsync()
    {
        var transfers = await _db.Set<WalletTransferPending>()
            .Include(t => t.FromBox).ThenInclude(b => b.CompanyPreset)
            .Include(t => t.ToBox).ThenInclude(b => b.CompanyPreset)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
        return await RespondAsync(transfers);
    }

    public async Task<WalletTransferPendingResponse?> GetByIdAsync(long id)
    {
        var t = await LoadAsync(id);
        return t == null ? null : (await RespondAsync([t])).Single();
    }

    public async Task<WalletTransferPendingResponse> CreateAsync(CreateWalletTransferPendingRequest req, long userId)
    {
        var boxes = await _db.Set<PaymentBox>()
            .Where(b => b.Id == req.FromBoxId || b.Id == req.ToBoxId)
            .ToDictionaryAsync(b => b.Id);
        if (req.FromBoxId == req.ToBoxId || !boxes.TryGetValue(req.FromBoxId, out var fromBox) || !boxes.TryGetValue(req.ToBoxId, out var toBox))
            throw new InvalidOperationException("Pick two different wallets.");
        if (req.WithdrawAmount <= 0) throw new InvalidOperationException("The amount must be above 0.");
        var from = fromBox.Currency;
        var to = toBox.Currency;

        // The rate is "1 base = rate other" (WalletTransferPending.RateBase); the amount that arrives is worked out here.
        var same = WalletTransferPending.SameCurrency(from, to);
        var rate = same ? null : req.ExchangeRate is > 0 ? req.ExchangeRate : null;
        var transfer = new WalletTransferPending
        {
            FromBoxId = req.FromBoxId,
            ToBoxId = req.ToBoxId,
            WithdrawAmount = req.WithdrawAmount,
            DepositAmount = same || rate != null ? WalletTransferPending.Convert(req.WithdrawAmount, from, to, rate) : req.DepositAmount,
            ExchangeRate = rate,
            RateBaseCurrency = rate != null ? WalletTransferPending.RateBase(from, to) : null,
            WireFeePaidBy = WireFeePayer.Normalize(req.WireFeePaidBy) ?? WireFeePayer.ForTransfer(fromBox.WireFeePaidBy),
            Notes = req.Notes,
            CreatedByUserId = userId,
            Status = Accepted,
            AcceptedByUserId = userId,
            AcceptedAt = DateTime.UtcNow,
        };
        _db.Set<WalletTransferPending>().Add(transfer);
        await _db.SaveChangesAsync();
        return (await GetByIdAsync(transfer.Id))!;
    }

    public async Task<bool> UploadPopAsync(long id, IFormFile file, long userId, string storageRoot)
    {
        var transfer = await LoadAsync(id);
        if (transfer == null || transfer.Status is not (Accepted or Review)) return false;

        await using (var stream = file.OpenReadStream())
            transfer.PopFileName = await SavePopAsync(id, file.FileName, stream, storageRoot);
        transfer.PopAt = DateTime.UtcNow;
        transfer.Status = Review;
        await PopReading.QueueAsync(_db, PopReading.Transfer, id);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<string?> StoreFromWarehouseAsync(long id, decimal? rate, decimal? bocRate, string? rateAt,
        string by, DateTime at, string fileName, byte[] pop, string storageRoot)
    {
        var transfer = await LoadAsync(id);
        if (transfer == null) return "This transfer was removed in the office.";
        var label = $"Transfer #{id}";
        if (transfer.Status == Completed)
            // Accepted after an earlier sync whose ack was lost: nothing to do again.
            return transfer.WarehouseBy != null ? null : $"{label}: already completed in the office.";

        var from = transfer.FromBox.Currency;
        var to = transfer.ToBox.Currency;
        if (!WalletTransferPending.SameCurrency(from, to) && rate is > 0)
        {
            if (decimal.Round(rate.Value, 6) != rate.Value) return $"{label}: the rate has more than six decimals.";
            // The warehouse saved the rate of the moment it got the POP: it replaces the office's guess.
            transfer.ExchangeRate = rate;
            transfer.RateBaseCurrency = WalletTransferPending.RateBase(from, to);
            transfer.DepositAmount = WalletTransferPending.Convert(transfer.WithdrawAmount, from, to, rate);
            transfer.BocRate = bocRate;
            transfer.RateAt = rateAt?.Length > 50 ? rateAt[..50] : rateAt;
        }

        transfer.PopFileName = await SavePopAsync(id, fileName, new MemoryStream(pop), storageRoot);
        transfer.PopAt = at == default ? DateTime.UtcNow : at;
        transfer.WarehouseBy = string.IsNullOrWhiteSpace(by) ? "Warehouse" : by.Trim()[..Math.Min(200, by.Trim().Length)];
        transfer.Status = Review;
        await PopReading.QueueAsync(_db, PopReading.Transfer, id);
        await _db.SaveChangesAsync();
        return null;
    }

    public async Task<string?> AcceptAsync(long id, AcceptWalletTransferRequest req, long userId)
    {
        var transfer = await LoadAsync(id);
        if (transfer == null) return "Transfer not found.";
        if (transfer.Status != Review) return "This transfer has no POP to accept, or it is already completed.";

        var from = transfer.FromBox.Currency;
        var to = transfer.ToBox.Currency;
        if (!Money(req.Amount) || req.Amount <= 0) return "The amount must be above 0 with at most two decimals.";
        var rate = req.ExchangeRate;
        if (WalletTransferPending.SameCurrency(from, to)) rate = null;
        else if (rate is null or <= 0 || decimal.Round(rate.Value, 6) != rate.Value)
        {
            var b = WalletTransferPending.RateBase(from, to);
            return $"Enter the rate as {(b == from ? to : from)} per 1 {b} (up to six decimals).";
        }
        if (!Fee(req.HandlingFee) || !Fee(req.BankFee) || !Fee(req.SourceWireFee) || !Fee(req.DestWireFee))
            return "The fees must be 0 or more with at most two decimals.";
        var received = WalletTransferPending.Convert(req.Amount, from, to, rate);
        if (received - req.DestWireFee <= 0) return "The destination's wire fee is more than what arrives.";

        transfer.WithdrawAmount = req.Amount;
        transfer.DepositAmount = received;
        transfer.ExchangeRate = rate;
        transfer.RateBaseCurrency = rate != null ? WalletTransferPending.RateBase(from, to) : null;
        transfer.HandlingFee = req.HandlingFee;
        transfer.BankFee = req.BankFee;
        transfer.SourceWireFee = req.SourceWireFee;
        transfer.WireFee = req.DestWireFee;
        transfer.CompletedByUserId = userId;
        Execute(transfer, transfer.PopAt);
        await _db.SaveChangesAsync();
        return null;
    }

    public async Task<(Stream Stream, string FileName, string MimeType)?> GetPopFileAsync(long id, string storageRoot)
    {
        var transfer = await _db.Set<WalletTransferPending>().FindAsync(id);
        if (transfer?.PopFileName == null) return null;
        var filePath = PopPath(storageRoot, id, transfer.PopFileName);
        if (!File.Exists(filePath)) return null;
        return (File.OpenRead(filePath), transfer.PopFileName, MimeOf(transfer.PopFileName));
    }

    public static string MimeOf(string name) => Path.GetExtension(name).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        ".bmp" => "image/bmp",
        _ => "application/octet-stream",
    };

    /// <summary>The rate as "1 RateBase = rate": old rows kept "1 From = rate To".</summary>
    public static decimal? BaseRate(WalletTransferPending t)
    {
        if (t.ExchangeRate is not > 0 || WalletTransferPending.SameCurrency(t.FromBox.Currency, t.ToBox.Currency)) return null;
        if (t.RateBaseCurrency != null) return t.ExchangeRate;
        var fromIsBase = WalletTransferPending.SameCurrency(WalletTransferPending.RateBase(t.FromBox.Currency, t.ToBox.Currency), t.FromBox.Currency);
        return fromIsBase ? t.ExchangeRate : Math.Round(1m / t.ExchangeRate.Value, 6, MidpointRounding.AwayFromZero);
    }

    public static TransferProposal Propose(WalletTransferPending t, PopReading? reading) => PopFees.Transfer(
        t.FromBox.Currency, t.ToBox.Currency, t.WithdrawAmount, BaseRate(t),
        t.FromBox.HandlingFee, t.FromBox.WireFee, t.WireFeePaidBy ?? WireFeePayer.ForTransfer(t.FromBox.WireFeePaidBy), reading);

    private Task<WalletTransferPending?> LoadAsync(long id) => _db.Set<WalletTransferPending>()
        .Include(t => t.FromBox).ThenInclude(b => b.CompanyPreset)
        .Include(t => t.ToBox).ThenInclude(b => b.CompanyPreset)
        .FirstOrDefaultAsync(t => t.Id == id);

    private static async Task<string> SavePopAsync(long id, string originalName, Stream content, string storageRoot)
    {
        var dir = Path.Combine(storageRoot, "WalletTransfers", id.ToString(), "pop");
        Directory.CreateDirectory(dir);
        var ext = Path.GetExtension(originalName);
        if (ext.Length > 10) ext = string.Empty;
        var fileName = $"pop_{DateTime.UtcNow:yyyyMMddHHmmssfff}{ext}";
        await using (var stream = new FileStream(Path.Combine(dir, fileName), FileMode.Create))
            await content.CopyToAsync(stream);
        return fileName;
    }

    /// <summary>
    /// The wallet-to-wallet rows, saved with the caller's SaveChanges (one transaction). Source: −amount, and Bank Fee
    /// rows for the handling fee, the fee the POP showed and its part of the wire fee; destination: +converted amount and
    /// a Bank Fee row for its part of the wire fee.
    /// Amounts are already in each wallet's base currency — do NOT set ExchangeRate on the transactions or the balance
    /// formula (Amount * ExchangeRate) would double-count. The rate goes in Notes for display only.
    /// </summary>
    private void Execute(WalletTransferPending transfer, DateTime? at = null)
    {
        var now = at ?? DateTime.UtcNow;
        var fromName = BoxName(transfer.FromBox);
        var toName = BoxName(transfer.ToBox);
        var rateNote = transfer.RateText() is { } r ? $" (Rate: {r})" : "";
        var userNote = transfer.Notes != null ? $" — {transfer.Notes}" : "";
        var byNote = transfer.WarehouseBy != null ? $" · warehouse: {transfer.WarehouseBy}" : "";

        var rows = new List<PaymentTransaction>
        {
            new()
            {
                PaymentBoxId = transfer.FromBoxId, Type = "Withdraw", Amount = transfer.WithdrawAmount,
                FromType = "Wallet", ToType = "Wallet", ToPaymentBoxId = transfer.ToBoxId,
                Notes = $"Wallet Transfer → {toName}{rateNote}{userNote}{byNote}", IsAuto = false, CreatedAt = now,
            },
            new()
            {
                PaymentBoxId = transfer.ToBoxId, Type = "Deposit", Amount = transfer.DepositAmount,
                FromType = "Wallet", ToType = "Wallet", ToPaymentBoxId = transfer.FromBoxId,
                Notes = $"Wallet Transfer ← {fromName}{rateNote}{userNote}{byNote}", IsAuto = false, CreatedAt = now,
            },
        };
        void Fee(long boxId, string currency, decimal? amount, string what)
        {
            if (amount is not > 0) return;
            rows.Add(new PaymentTransaction
            {
                PaymentBoxId = boxId, Type = "Withdraw", Amount = amount.Value, TxCurrency = currency,
                FromType = "MotherWallet", ToType = "BankFee", IsAuto = false, CreatedAt = now, Notes = what,
            });
        }
        Fee(transfer.FromBoxId, transfer.FromBox.Currency, transfer.HandlingFee, $"Handling fee — wallet transfer #{transfer.Id} → {toName}");
        Fee(transfer.FromBoxId, transfer.FromBox.Currency, transfer.BankFee, $"Bank fee on the POP — wallet transfer #{transfer.Id} → {toName}");
        Fee(transfer.FromBoxId, transfer.FromBox.Currency, transfer.SourceWireFee,
            $"Wire fee ({PayerText(transfer.WireFeePaidBy)}) — wallet transfer #{transfer.Id} → {toName}");
        Fee(transfer.ToBoxId, transfer.ToBox.Currency, transfer.WireFee,
            $"Wire fee ({PayerText(transfer.WireFeePaidBy)}) — wallet transfer #{transfer.Id} ← {fromName}");

        transfer.Status = Completed;
        transfer.CompletedAt = DateTime.UtcNow;
        _db.Set<PaymentTransaction>().AddRange(rows);
    }

    private static string PayerText(string? payer) => WireFeePayer.Normalize(payer) switch
    {
        WireFeePayer.Ours => "all ours",
        WireFeePayer.Split => "50/50",
        WireFeePayer.Theirs => "all theirs",
        _ => "wallet",
    };

    private static bool Money(decimal v) => decimal.Round(v, 2) == v;
    private static bool Fee(decimal v) => v >= 0 && Money(v);

    private static string BoxName(PaymentBox box)
        => string.IsNullOrWhiteSpace(box.Name) ? box.CompanyPreset?.Name ?? $"Wallet {box.Id}" : box.Name;

    private async Task<List<WalletTransferPendingResponse>> RespondAsync(List<WalletTransferPending> transfers)
    {
        var userIds = transfers.Select(t => t.CreatedByUserId).Distinct().ToList();
        var users = await _db.Set<User>()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name);
        var ids = transfers.Select(t => t.Id).ToList();
        var readings = await _db.Set<PopReading>().AsNoTracking()
            .Where(r => r.Kind == PopReading.Transfer && ids.Contains(r.RefId))
            .ToDictionaryAsync(r => r.RefId);

        return transfers.Select(t =>
        {
            var reading = readings.GetValueOrDefault(t.Id);
            var from = t.FromBox.Currency;
            var to = t.ToBox.Currency;
            var rateBase = WalletTransferPending.SameCurrency(from, to) ? null : WalletTransferPending.RateBase(from, to);
            return new WalletTransferPendingResponse(t.Id,
                t.FromBoxId, BoxName(t.FromBox), from,
                t.ToBoxId, BoxName(t.ToBox), to,
                t.WithdrawAmount, t.DepositAmount, t.ExchangeRate, t.RateText(), t.HandlingFee, t.WireFee,
                t.Notes, t.Status, t.PopFileName, t.RejectionNote,
                t.CreatedByUserId, users.GetValueOrDefault(t.CreatedByUserId, "Unknown"),
                t.CreatedAt, t.AcceptedAt, t.CompletedAt, t.WarehouseBy,
                t.SourceWireFee, t.BankFee, t.WireFeePaidBy ?? WireFeePayer.ForTransfer(t.FromBox.WireFeePaidBy), t.BocRate, t.RateAt, t.PopAt,
                rateBase, rateBase == null ? null : WalletTransferPending.SameCurrency(rateBase, from) ? to : from,
                PopReadingDto.From(reading),
                t.Status == Review ? Propose(t, reading) : null);
        }).ToList();
    }
}
