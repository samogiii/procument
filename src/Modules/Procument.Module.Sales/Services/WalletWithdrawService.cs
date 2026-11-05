using Microsoft.EntityFrameworkCore;
using Procument.Module.Identity.Entities;
using Procument.Module.Purchasing.Entities;
using Procument.Module.Sales.DTOs;
using Procument.Module.Sales.Entities;

namespace Procument.Module.Sales.Services;

public interface IWalletWithdrawService
{
    Task<List<WalletWithdrawResponse>> GetAllAsync();
    Task<WalletWithdrawResponse> CreateAsync(CreateWalletWithdrawRequest req, long userId);
    /// <summary>Only while it waits for the warehouse. False = not found or already paid.</summary>
    Task<bool> DeleteAsync(long id);
    /// <summary>The warehouse phone's POP and the rate saved with it: the withdraw goes to Review. Null = stored, otherwise why not.</summary>
    Task<string?> StoreFromWarehouseAsync(long id, decimal? rate, decimal? bocRate, string? rateAt,
        string by, DateTime at, string fileName, byte[] pop, string storageRoot);
    /// <summary>The office's final numbers: writes the withdraw and its fee rows. Null = done, otherwise why not.</summary>
    Task<string?> AcceptAsync(long id, AcceptWalletWithdrawRequest req);
    Task<(Stream Stream, string FileName)?> GetPopFileAsync(long id, string storageRoot);
}

/// <summary>
/// Withdraws the warehouse pays: Pending (waiting for the POP) → Review (POP in, the local POP reader reads it) →
/// Completed (the office accepted the numbers). The rows written are the same as a manual withdraw (Add Transaction)
/// plus Bank Fee rows for the handling fee, the fee the POP showed and our part of the wire fee, so wallet balances,
/// PR payment and BI treat them as usual. POPs live next to the wallet transfer ones: {storage root}/WalletWithdraws/{id}/pop.
/// </summary>
public class WalletWithdrawService : IWalletWithdrawService
{
    private readonly DbContext _db;

    public WalletWithdrawService(DbContext db) => _db = db;

    public static string PopPath(string storageRoot, long id, string fileName)
        => Path.Combine(storageRoot, "WalletWithdraws", id.ToString(), "pop", fileName);

    public async Task<List<WalletWithdrawResponse>> GetAllAsync()
    {
        var rows = await (
            from w in _db.Set<WalletWithdrawPending>().AsNoTracking()
            join u in _db.Set<User>() on w.CreatedByUserId equals u.Id into users
            from u in users.DefaultIfEmpty()
            join pr in _db.Set<PaymentRequest>() on w.PaymentRequestId equals pr.Id into prs
            from pr in prs.DefaultIfEmpty()
            orderby w.CreatedAt descending
            select new
            {
                W = w, WalletName = w.PaymentBox.Name, Company = w.PaymentBox.CompanyPreset.Name, WalletCurrency = w.PaymentBox.Currency,
                WalletHandling = w.PaymentBox.HandlingFee, WalletWire = w.PaymentBox.WireFee, WalletPayer = w.PaymentBox.WireFeePaidBy,
                Supplier = w.ToSupplier != null ? w.ToSupplier.Name : null, CreatedBy = u != null ? u.Name : null,
                PrNumber = pr != null ? pr.PRId : null,
            })
            .ToListAsync();

        var ids = rows.Select(r => r.W.Id).ToList();
        var readings = await _db.Set<PopReading>().AsNoTracking()
            .Where(r => r.Kind == PopReading.Withdraw && ids.Contains(r.RefId))
            .ToDictionaryAsync(r => r.RefId);

        return rows.Select(r =>
        {
            var reading = readings.GetValueOrDefault(r.W.Id);
            var payer = r.W.WireFeePaidBy ?? WireFeePayer.ForWithdraw(r.WalletPayer);
            return new WalletWithdrawResponse(
                r.W.Id, r.W.PaymentBoxId, string.IsNullOrWhiteSpace(r.WalletName) ? r.Company ?? $"Wallet {r.W.PaymentBoxId}" : r.WalletName,
                r.WalletCurrency, r.W.Amount, r.W.Currency ?? r.WalletCurrency, r.W.OfficeRate,
                r.W.ToType, r.W.ToType == "Supplier" ? r.Supplier : "Mother Wallet", r.W.PaymentRequestId, r.PrNumber,
                r.W.Base, r.W.Notes, r.W.Status, r.CreatedBy ?? "Unknown", r.W.CreatedAt,
                r.W.PopFileName, r.W.ExchangeRate, r.W.HandlingFee, r.W.WireFee, r.W.WalletAmount, r.W.WarehouseBy, r.W.CompletedAt,
                r.W.BankFee, payer, r.W.BocRate, r.W.RateAt, r.W.PopAt,
                PopReadingDto.From(reading),
                r.W.Status == WalletWithdrawPending.Review
                    ? PopFees.Withdraw(r.WalletCurrency, r.W.Currency ?? r.WalletCurrency, r.W.Amount, r.W.ExchangeRate ?? r.W.OfficeRate,
                        r.WalletHandling, r.WalletWire, payer, reading)
                    : null);
        }).ToList();
    }

    public async Task<WalletWithdrawResponse> CreateAsync(CreateWalletWithdrawRequest req, long userId)
    {
        var wallet = await _db.Set<PaymentBox>().Where(b => b.Id == req.PaymentBoxId)
            .Select(b => new { b.Currency, b.WireFeePaidBy }).FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Wallet not found.");
        if (req.Amount <= 0 || decimal.Round(req.Amount, 2) != req.Amount)
            throw new InvalidOperationException("The amount must be above 0 with at most two decimals.");
        if (req.ToType is not ("MotherWallet" or "Supplier")) throw new InvalidOperationException("Withdraw to the Mother Wallet or a supplier.");
        if (req.ToType == "Supplier" && req.ToSupplierId == null) throw new InvalidOperationException("Pick the supplier.");

        var same = WalletWithdrawPending.SameCurrency(req.Currency, wallet.Currency);
        var withdraw = new WalletWithdrawPending
        {
            PaymentBoxId = req.PaymentBoxId,
            Amount = req.Amount,
            Currency = same ? null : req.Currency,
            OfficeRate = same ? null : req.OfficeRate is > 0 ? req.OfficeRate : null,
            ToType = req.ToType,
            ToSupplierId = req.ToType == "Supplier" ? req.ToSupplierId : null,
            PaymentRequestId = req.ToType == "Supplier" ? req.PaymentRequestId : null,
            Base = TransactionBases.Normalize(req.Base),
            Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim(),
            WireFeePaidBy = WireFeePayer.Normalize(req.WireFeePaidBy) ?? WireFeePayer.ForWithdraw(wallet.WireFeePaidBy),
            CreatedByUserId = userId,
        };
        _db.Set<WalletWithdrawPending>().Add(withdraw);
        await _db.SaveChangesAsync();
        return (await GetAllAsync()).First(w => w.Id == withdraw.Id);
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var withdraw = await _db.Set<WalletWithdrawPending>().FindAsync(id);
        if (withdraw == null || withdraw.Status != WalletWithdrawPending.Pending) return false;
        _db.Set<WalletWithdrawPending>().Remove(withdraw);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<string?> StoreFromWarehouseAsync(long id, decimal? rate, decimal? bocRate, string? rateAt,
        string by, DateTime at, string fileName, byte[] pop, string storageRoot)
    {
        var w = await _db.Set<WalletWithdrawPending>().Include(x => x.PaymentBox).FirstOrDefaultAsync(x => x.Id == id);
        if (w == null) return $"Withdraw #{id} was removed in the office.";
        // Accepted after an earlier sync whose ack was lost: nothing to do again.
        if (w.Status == WalletWithdrawPending.Completed) return null;

        if (!WalletWithdrawPending.SameCurrency(w.Currency, w.PaymentBox.Currency) && rate is > 0)
        {
            if (decimal.Round(rate.Value, 6) != rate.Value) return $"Withdraw #{id}: the rate has more than six decimals.";
            // The warehouse saved the rate of the moment it got the POP.
            w.ExchangeRate = rate;
            w.BocRate = bocRate;
            w.RateAt = rateAt?.Length > 50 ? rateAt[..50] : rateAt;
        }

        var dir = Path.Combine(storageRoot, "WalletWithdraws", id.ToString(), "pop");
        Directory.CreateDirectory(dir);
        var ext = Path.GetExtension(fileName);
        if (ext.Length > 10) ext = string.Empty;
        w.PopFileName = $"pop_{DateTime.UtcNow:yyyyMMddHHmmssfff}{ext}";
        await File.WriteAllBytesAsync(Path.Combine(dir, w.PopFileName), pop);
        w.PopAt = at == default ? DateTime.UtcNow : at;
        w.WarehouseBy = string.IsNullOrWhiteSpace(by) ? "Warehouse" : by.Trim()[..Math.Min(200, by.Trim().Length)];
        w.Status = WalletWithdrawPending.Review;
        await PopReading.QueueAsync(_db, PopReading.Withdraw, id);
        await _db.SaveChangesAsync();
        return null;
    }

    public async Task<string?> AcceptAsync(long id, AcceptWalletWithdrawRequest req)
    {
        var w = await _db.Set<WalletWithdrawPending>().Include(x => x.PaymentBox).FirstOrDefaultAsync(x => x.Id == id);
        if (w == null) return "Withdraw not found.";
        if (w.Status != WalletWithdrawPending.Review) return "This withdraw has no POP to accept, or it is already recorded.";

        var walletCurrency = w.PaymentBox.Currency;
        if (req.Amount <= 0 || decimal.Round(req.Amount, 2) != req.Amount) return "The amount must be above 0 with at most two decimals.";
        var rate = req.ExchangeRate;
        if (WalletWithdrawPending.SameCurrency(w.Currency, walletCurrency)) rate = null;
        else if (rate is null or <= 0 || decimal.Round(rate.Value, 6) != rate.Value)
            return $"Enter the rate as {walletCurrency} per 1 {w.Currency} (up to six decimals).";
        if (new[] { req.HandlingFee, req.BankFee, req.WireFee }.Any(f => f < 0 || decimal.Round(f, 2) != f))
            return "The fees must be 0 or more with at most two decimals.";

        var now = w.PopAt ?? DateTime.UtcNow;
        var by = w.WarehouseBy != null ? $" · warehouse: {w.WarehouseBy}" : "";
        w.Amount = req.Amount;
        w.ExchangeRate = rate;
        w.HandlingFee = req.HandlingFee;
        w.BankFee = req.BankFee;
        w.WireFee = req.WireFee;
        w.WalletAmount = WalletWithdrawPending.ToWallet(w.Amount, rate);

        var tx = new PaymentTransaction
        {
            PaymentBoxId = w.PaymentBoxId, Type = "Withdraw", Amount = w.Amount,
            FromType = "MotherWallet", ToType = w.ToType, ToSupplierId = w.ToSupplierId, PaymentRequestId = w.PaymentRequestId,
            TxCurrency = w.Currency, ExchangeRate = rate, Base = w.Base, IsAuto = false, CreatedAt = now,
            Notes = $"{w.Notes ?? "Withdraw"}{by}",
        };
        _db.Set<PaymentTransaction>().Add(tx);
        var fees = new List<PaymentTransaction>();
        void Fee(decimal amount, string what)
        {
            if (amount <= 0) return;
            var row = new PaymentTransaction
            {
                PaymentBoxId = w.PaymentBoxId, Type = "Withdraw", Amount = amount, TxCurrency = walletCurrency,
                FromType = "MotherWallet", ToType = "BankFee", PaymentRequestId = w.PaymentRequestId, Base = w.Base,
                IsAuto = false, CreatedAt = now, Notes = $"{what} — withdraw #{id}",
            };
            fees.Add(row);
            _db.Set<PaymentTransaction>().Add(row);
        }
        Fee(req.HandlingFee, "Handling fee");
        Fee(req.BankFee, "Bank fee on the POP");
        Fee(req.WireFee, $"Wire fee ({(WireFeePayer.Normalize(w.WireFeePaidBy) == WireFeePayer.Split ? "50/50" : "ours")})");
        w.Status = WalletWithdrawPending.Completed;
        w.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        w.TransactionId = tx.Id;
        w.FeeTransactionId = fees.FirstOrDefault()?.Id;
        await _db.SaveChangesAsync();
        return null;
    }

    public async Task<(Stream Stream, string FileName)?> GetPopFileAsync(long id, string storageRoot)
    {
        var w = await _db.Set<WalletWithdrawPending>().FindAsync(id);
        if (w?.PopFileName == null) return null;
        var path = PopPath(storageRoot, id, w.PopFileName);
        return File.Exists(path) ? (File.OpenRead(path), w.PopFileName) : null;
    }
}
