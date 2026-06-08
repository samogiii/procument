using Microsoft.EntityFrameworkCore;
using Procument.Module.Catalog.Entities;
using Procument.Module.Purchasing.Entities;
using Procument.Module.Sales.DTOs;
using Procument.Module.Sales.Entities;
using Procument.Shared.Services;

namespace Procument.Module.Sales.Services;

public class PaymentBoxService : IPaymentBoxService, IPaymentLedgerService
{
    private readonly DbContext _db;

    public PaymentBoxService(DbContext db)
    {
        _db = db;
    }

    // ── Queries ─────────────────────────────────────────────────────────────

    public async Task<List<PaymentBoxSummaryResponse>> GetAllAsync(long? companyPresetId = null)
    {
        var query = _db.Set<PaymentBox>()
            .Include(b => b.CompanyPreset)
            .Include(b => b.Transactions)
            .OrderBy(b => b.CreatedAt)
            .AsQueryable();

        if (companyPresetId.HasValue)
            query = query.Where(b => b.CompanyPresetId == companyPresetId.Value);

        var boxes = await query.ToListAsync();
        return boxes.Select(ToSummary).ToList();
    }

    public async Task<PaymentBoxDetailResponse?> GetByIdAsync(long id)
    {
        var box = await _db.Set<PaymentBox>()
            .Include(b => b.CompanyPreset)
            .Include(b => b.Transactions)
                .ThenInclude(t => t.FromCustomer)
            .Include(b => b.Transactions)
                .ThenInclude(t => t.ToSupplier)
            .Include(b => b.Transactions)
                .ThenInclude(t => t.Invoice)
            .Include(b => b.Transactions)
                .ThenInclude(t => t.PaymentRequest)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (box == null) return null;

        // Load sibling wallet names for wallet-to-wallet transfers
        var walletIds = box.Transactions
            .Where(t => t.ToPaymentBoxId.HasValue)
            .Select(t => t.ToPaymentBoxId!.Value)
            .Distinct().ToList();
        var walletNames = await _db.Set<PaymentBox>()
            .Include(b => b.CompanyPreset)
            .Where(b => walletIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.CompanyPreset?.Name ?? $"Wallet {b.Id}");

        var ordered = box.Transactions.OrderBy(t => t.CreatedAt).ToList();
        decimal running = 0;
        var rows = ordered.Select(t =>
        {
            decimal factor = t.ExchangeRate ?? 1m;
            running += t.Type == "Deposit" ? t.Amount * factor : -(t.Amount * factor);
            var otherWallet = t.ToPaymentBoxId.HasValue && walletNames.TryGetValue(t.ToPaymentBoxId.Value, out var n) ? n : null;
            return new PaymentTransactionRow(
                t.Id,
                t.Type,
                t.Type == "Deposit" ? t.Amount : null,
                t.Type == "Withdraw" ? t.Amount : null,
                t.FromType,
                t.FromType == "Customer"
                    ? (t.FromCustomer?.CustomerCode ?? t.FromCustomer?.Name)
                    : t.FromType == "Wallet" ? (otherWallet ?? "Wallet Transfer") : "Mother Wallet",
                t.FromCustomerId,
                t.ToType,
                t.ToType == "Supplier"
                    ? t.ToSupplier?.Name
                    : t.ToType == "Wallet" ? (otherWallet ?? "Wallet Transfer") : "Mother Wallet",
                t.ToSupplierId,
                t.Invoice?.InvoiceNumber,
                t.InvoiceId,
                t.PaymentRequest?.PRId != null ? $"PR-{t.PaymentRequest.PRId}" : null,
                t.PaymentRequestId,
                t.PaymentRequest?.POId,
                t.Notes,
                t.IsAuto,
                t.CreatedAt,
                running,
                t.TxCurrency,
                t.ExchangeRate);
        }).ToList();

        var totalDeposit = ordered.Where(t => t.Type == "Deposit").Sum(t => t.Amount * (t.ExchangeRate ?? 1m));
        var totalWithdraw = ordered.Where(t => t.Type == "Withdraw").Sum(t => t.Amount * (t.ExchangeRate ?? 1m));

        return new PaymentBoxDetailResponse(
            box.Id,
            box.CompanyPreset?.Name ?? "",
            box.Currency,
            totalDeposit,
            totalWithdraw,
            totalDeposit - totalWithdraw,
            rows);
    }

    public async Task<List<AllTransactionRow>> GetAllTransactionsAsync()
    {
        var boxes = await _db.Set<PaymentBox>()
            .Include(b => b.CompanyPreset)
            .Include(b => b.Transactions)
                .ThenInclude(t => t.FromCustomer)
            .Include(b => b.Transactions)
                .ThenInclude(t => t.ToSupplier)
            .Include(b => b.Transactions)
                .ThenInclude(t => t.Invoice)
            .Include(b => b.Transactions)
                .ThenInclude(t => t.PaymentRequest)
            .ToListAsync();

        var allBoxNames = boxes.ToDictionary(b => b.Id, b => b.CompanyPreset?.Name ?? $"Wallet {b.Id}");
        var result = new List<AllTransactionRow>();

        foreach (var box in boxes)
        {
            decimal running = 0;
            foreach (var t in box.Transactions.OrderBy(t => t.CreatedAt))
            {
                decimal factor = t.ExchangeRate ?? 1m;
                running += t.Type == "Deposit" ? t.Amount * factor : -(t.Amount * factor);
                var otherWallet = t.ToPaymentBoxId.HasValue && allBoxNames.TryGetValue(t.ToPaymentBoxId.Value, out var n) ? n : null;
                result.Add(new AllTransactionRow(
                    t.Id,
                    box.Id,
                    box.CompanyPreset?.Name ?? "",
                    box.Currency,
                    t.Type == "Deposit" ? t.Amount : null,
                    t.Type == "Withdraw" ? t.Amount : null,
                    t.FromType,
                    t.Type == "Deposit"
                        ? (t.FromType == "Customer"
                            ? (t.FromCustomer?.CustomerCode ?? t.FromCustomer?.Name)
                            : t.FromType == "Wallet" ? (otherWallet ?? "Wallet Transfer") : "Mother Wallet")
                        : null,
                    t.FromCustomerId,
                    t.ToType,
                    t.Type == "Withdraw"
                        ? (t.ToType == "Supplier"
                            ? t.ToSupplier?.Name
                            : t.ToType == "Wallet" ? (otherWallet ?? "Wallet Transfer") : "Mother Wallet")
                        : null,
                    t.ToSupplierId,
                    t.Invoice?.InvoiceNumber,
                    t.InvoiceId,
                    t.PaymentRequest?.PRId != null ? $"PR-{t.PaymentRequest.PRId}" : null,
                    t.PaymentRequestId,
                    t.PaymentRequest?.POId,
                    t.Notes,
                    t.IsAuto,
                    t.CreatedAt,
                    running,
                    t.TxCurrency,
                    t.ExchangeRate));
            }
        }

        return result.OrderBy(r => r.CreatedAt).ToList();
    }

    // ── Mutations ────────────────────────────────────────────────────────────

    public async Task<PaymentBoxSummaryResponse> CreateBoxAsync(CreatePaymentBoxRequest req)
    {
        var box = new PaymentBox
        {
            CompanyPresetId = req.CompanyPresetId,
            Currency = req.Currency,
            Name = req.Name,
            CreatedAt = DateTime.UtcNow,
            BankName = req.BankName,
            BankAddress = req.BankAddress,
            AccountNumber = req.AccountNumber,
            BeneficiaryName = req.BeneficiaryName,
            SwiftCode = req.SwiftCode,
        };
        _db.Set<PaymentBox>().Add(box);
        await _db.SaveChangesAsync();

        await _db.Entry(box).Reference(b => b.CompanyPreset).LoadAsync();
        return ToSummary(box);
    }

    public async Task<PaymentBoxSummaryResponse?> RenameBoxAsync(long id, string name)
    {
        var box = await _db.Set<PaymentBox>()
            .Include(b => b.CompanyPreset)
            .Include(b => b.Transactions)
            .FirstOrDefaultAsync(b => b.Id == id);
        if (box == null) return null;
        box.Name = name;
        await _db.SaveChangesAsync();
        return ToSummary(box);
    }

    public async Task<PaymentBoxSummaryResponse?> UpdateBankDetailsAsync(long id, UpdateWalletBankDetailsRequest req)
    {
        var box = await _db.Set<PaymentBox>()
            .Include(b => b.CompanyPreset)
            .Include(b => b.Transactions)
            .FirstOrDefaultAsync(b => b.Id == id);
        if (box == null) return null;
        box.BankName = req.BankName;
        box.BankAddress = req.BankAddress;
        box.AccountNumber = req.AccountNumber;
        box.BeneficiaryName = req.BeneficiaryName;
        box.SwiftCode = req.SwiftCode;
        await _db.SaveChangesAsync();
        return ToSummary(box);
    }

    public async Task<List<PaymentBoxSummaryResponse>> GetBoxesForCustomerAsync(long customerId)
    {
        var customer = await _db.Set<Customer>()
            .FirstOrDefaultAsync(c => c.Id == customerId);
        if (customer?.Base == null) return [];

        var preset = await _db.Set<CompanyPreset>()
            .FirstOrDefaultAsync(p => p.SortOrder == customer.Base);
        if (preset == null) return [];

        return await GetAllAsync(preset.Id);
    }

    public async Task<List<WalletSelectionResponse>> GetSimpleListAsync()
    {
        return await _db.Set<PaymentBox>()
            .Include(b => b.CompanyPreset)
            .OrderBy(b => b.CompanyPreset.Name)
            .ThenBy(b => b.Name)
            .Select(b => new WalletSelectionResponse(
                b.Id,
                string.IsNullOrWhiteSpace(b.Name) ? b.CompanyPreset.Name : b.Name,
                b.CompanyPreset.Name,
                b.Currency))
            .ToListAsync();
    }

    public async Task<bool> DeleteBoxAsync(long id)
    {
        var box = await _db.Set<PaymentBox>().FindAsync(id);
        if (box == null) return false;
        _db.Set<PaymentBox>().Remove(box);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<PaymentTransactionRow?> AddTransactionAsync(long boxId, CreateTransactionRequest req)
    {
        var box = await _db.Set<PaymentBox>().FindAsync(boxId);
        if (box == null) return null;

        var tx = new PaymentTransaction
        {
            PaymentBoxId = boxId,
            Type = req.Type,
            Amount = req.Amount,
            FromType = req.FromType,
            FromCustomerId = req.FromCustomerId,
            ToType = req.ToType,
            ToSupplierId = req.ToSupplierId,
            InvoiceId = req.InvoiceId,
            PaymentRequestId = req.PaymentRequestId,
            Notes = req.Notes,
            IsAuto = false,
            TxCurrency = req.Currency,
            ExchangeRate = req.ExchangeRate,
            ToPaymentBoxId = req.ToPaymentBoxId,
            CreatedAt = DateTime.UtcNow
        };
        _db.Set<PaymentTransaction>().Add(tx);
        await _db.SaveChangesAsync();

        if (tx.FromCustomerId.HasValue)
            await _db.Entry(tx).Reference(t => t.FromCustomer).LoadAsync();
        if (tx.ToSupplierId.HasValue)
            await _db.Entry(tx).Reference(t => t.ToSupplier).LoadAsync();
        if (tx.InvoiceId.HasValue)
            await _db.Entry(tx).Reference(t => t.Invoice).LoadAsync();
        if (tx.PaymentRequestId.HasValue)
            await _db.Entry(tx).Reference(t => t.PaymentRequest).LoadAsync();

        var allTx = await _db.Set<PaymentTransaction>()
            .Where(t => t.PaymentBoxId == boxId)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();
        decimal running = 0;
        foreach (var t in allTx)
        {
            decimal factor = t.ExchangeRate ?? 1m;
            running += t.Type == "Deposit" ? t.Amount * factor : -(t.Amount * factor);
        }

        return new PaymentTransactionRow(
            tx.Id, tx.Type,
            tx.Type == "Deposit" ? tx.Amount : null,
            tx.Type == "Withdraw" ? tx.Amount : null,
            tx.FromType,
            tx.FromType == "Customer"
                ? (tx.FromCustomer?.CustomerCode ?? tx.FromCustomer?.Name)
                : tx.FromType == "Wallet" ? "Wallet Transfer" : "Mother Wallet",
            tx.FromCustomerId,
            tx.ToType,
            tx.ToType == "Supplier" ? tx.ToSupplier?.Name
                : tx.ToType == "Wallet" ? "Wallet Transfer" : "Mother Wallet",
            tx.ToSupplierId,
            tx.Invoice?.InvoiceNumber,
            tx.InvoiceId,
            tx.PaymentRequest?.PRId != null ? $"PR-{tx.PaymentRequest.PRId}" : null,
            tx.PaymentRequestId,
            tx.PaymentRequest?.POId,
            tx.Notes,
            tx.IsAuto,
            tx.CreatedAt,
            running,
            tx.TxCurrency,
            tx.ExchangeRate);
    }

    public async Task<PaymentTransactionRow?> UpdateTransactionAsync(long txId, UpdateTransactionRequest req)
    {
        var tx = await _db.Set<PaymentTransaction>().FindAsync(txId);
        if (tx == null) return null;

        tx.Type = req.Type;
        tx.Amount = req.Amount;
        tx.FromType = req.FromType;
        tx.FromCustomerId = req.FromCustomerId;
        tx.ToType = req.ToType;
        tx.ToSupplierId = req.ToSupplierId;
        tx.InvoiceId = req.InvoiceId;
        tx.PaymentRequestId = req.PaymentRequestId;
        tx.Notes = req.Notes;
        tx.TxCurrency = req.Currency;
        tx.ExchangeRate = req.ExchangeRate;
        tx.ToPaymentBoxId = req.ToPaymentBoxId;
        tx.CreatedAt = req.CreatedAt;

        await _db.SaveChangesAsync();

        if (tx.FromCustomerId.HasValue)
            await _db.Entry(tx).Reference(t => t.FromCustomer).LoadAsync();
        if (tx.ToSupplierId.HasValue)
            await _db.Entry(tx).Reference(t => t.ToSupplier).LoadAsync();
        if (tx.InvoiceId.HasValue)
            await _db.Entry(tx).Reference(t => t.Invoice).LoadAsync();
        if (tx.PaymentRequestId.HasValue)
            await _db.Entry(tx).Reference(t => t.PaymentRequest).LoadAsync();

        var allTx = await _db.Set<PaymentTransaction>()
            .Where(t => t.PaymentBoxId == tx.PaymentBoxId)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();
        decimal running = 0;
        foreach (var t in allTx)
        {
            decimal factor = t.ExchangeRate ?? 1m;
            running += t.Type == "Deposit" ? t.Amount * factor : -(t.Amount * factor);
        }

        return new PaymentTransactionRow(
            tx.Id, tx.Type,
            tx.Type == "Deposit" ? tx.Amount : null,
            tx.Type == "Withdraw" ? tx.Amount : null,
            tx.FromType,
            tx.FromType == "Customer"
                ? (tx.FromCustomer?.CustomerCode ?? tx.FromCustomer?.Name)
                : tx.FromType == "Wallet" ? "Wallet Transfer" : "Mother Wallet",
            tx.FromCustomerId,
            tx.ToType,
            tx.ToType == "Supplier" ? tx.ToSupplier?.Name
                : tx.ToType == "Wallet" ? "Wallet Transfer" : "Mother Wallet",
            tx.ToSupplierId,
            tx.Invoice?.InvoiceNumber,
            tx.InvoiceId,
            tx.PaymentRequest?.PRId != null ? $"PR-{tx.PaymentRequest.PRId}" : null,
            tx.PaymentRequestId,
            tx.PaymentRequest?.POId,
            tx.Notes,
            tx.IsAuto,
            tx.CreatedAt,
            running,
            tx.TxCurrency,
            tx.ExchangeRate);
    }

    public async Task<bool> DeleteTransactionAsync(long txId)
    {
        var tx = await _db.Set<PaymentTransaction>().FindAsync(txId);
        if (tx == null) return false;
        _db.Set<PaymentTransaction>().Remove(tx);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> TransferAsync(long sourceBoxId, WalletTransferRequest req)
    {
        var sourceBox = await _db.Set<PaymentBox>()
            .Include(b => b.CompanyPreset)
            .FirstOrDefaultAsync(b => b.Id == sourceBoxId);
        if (sourceBox == null) return false;

        var targetBox = await _db.Set<PaymentBox>()
            .Include(b => b.CompanyPreset)
            .FirstOrDefaultAsync(b => b.Id == req.ToBoxId);
        if (targetBox == null) return false;

        var now = DateTime.UtcNow;
        var sourceName = sourceBox.CompanyPreset?.Name ?? $"Box {sourceBoxId}";
        var targetName = targetBox.CompanyPreset?.Name ?? $"Box {req.ToBoxId}";

        var rateNote = req.ExchangeRate.HasValue
            ? $" (Rate: 1 {sourceBox.Currency} = {req.ExchangeRate} {targetBox.Currency})"
            : "";
        var userNote = req.Notes != null ? $" — {req.Notes}" : "";

        _db.Set<PaymentTransaction>().Add(new PaymentTransaction
        {
            PaymentBoxId = sourceBoxId,
            Type = "Withdraw",
            Amount = req.WithdrawAmount,
            FromType = "Wallet",
            ToType = "Wallet",
            ToPaymentBoxId = req.ToBoxId,
            Notes = $"Wallet Transfer → {targetName}{rateNote}{userNote}",
            IsAuto = false,
            CreatedAt = now
        });

        _db.Set<PaymentTransaction>().Add(new PaymentTransaction
        {
            PaymentBoxId = req.ToBoxId,
            Type = "Deposit",
            Amount = req.DepositAmount,
            FromType = "Wallet",
            ToType = "Wallet",
            ToPaymentBoxId = sourceBoxId,   // so target wallet knows where it came from
            Notes = $"Wallet Transfer ← {sourceName}{rateNote}{userNote}",
            IsAuto = false,
            CreatedAt = now
        });

        await _db.SaveChangesAsync();
        return true;
    }

    // ── Auto-deposit / Auto-withdraw ─────────────────────────────────────────

    public async Task TryAutoDepositAsync(long invoiceId, decimal amount, long customerId, string? currency = null, decimal? exchangeRate = null, long? explicitBoxId = null)
    {
        PaymentBox? box = null;

        // Prefer explicit box (set at PI creation time)
        if (explicitBoxId.HasValue)
            box = await _db.Set<PaymentBox>().FirstOrDefaultAsync(b => b.Id == explicitBoxId.Value);

        // Fallback: resolve via customer base → preset → first matching box
        if (box == null)
        {
            var customer = await _db.Set<Customer>()
                .FirstOrDefaultAsync(c => c.Id == customerId);
            if (customer?.Base == null) return;

            var preset = await _db.Set<CompanyPreset>()
                .FirstOrDefaultAsync(p => p.SortOrder == customer.Base);
            if (preset == null) return;

            box = await _db.Set<PaymentBox>()
                .FirstOrDefaultAsync(b => b.CompanyPresetId == preset.Id
                    && (currency == null || b.Currency == currency));

            // Fallback to any box for this preset when currency doesn't match
            if (box == null)
                box = await _db.Set<PaymentBox>()
                    .FirstOrDefaultAsync(b => b.CompanyPresetId == preset.Id);
        }

        if (box == null) return;

        _db.Set<PaymentTransaction>().Add(new PaymentTransaction
        {
            PaymentBoxId = box.Id,
            Type = "Deposit",
            Amount = amount,
            FromType = "Customer",
            FromCustomerId = customerId,
            ToType = "MotherWallet",
            InvoiceId = invoiceId,
            IsAuto = true,
            TxCurrency = currency,
            ExchangeRate = exchangeRate,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    public async Task TryAutoWithdrawAsync(long supplierId, decimal amount, long? companyPresetId, long? paymentRequestId, long? explicitBoxId = null)
    {
        PaymentBox? box = null;

        // Prefer explicit box override
        if (explicitBoxId.HasValue)
            box = await _db.Set<PaymentBox>().FirstOrDefaultAsync(b => b.Id == explicitBoxId.Value);

        // Fallback: first box for preset
        if (box == null)
        {
            if (companyPresetId == null) return;
            box = await _db.Set<PaymentBox>()
                .FirstOrDefaultAsync(b => b.CompanyPresetId == companyPresetId);
        }

        if (box == null) return;

        _db.Set<PaymentTransaction>().Add(new PaymentTransaction
        {
            PaymentBoxId = box.Id,
            Type = "Withdraw",
            Amount = amount,
            FromType = "MotherWallet",
            ToType = "Supplier",
            ToSupplierId = supplierId,
            PaymentRequestId = paymentRequestId,
            IsAuto = true,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static PaymentBoxSummaryResponse ToSummary(PaymentBox box)
    {
        var deposits = box.Transactions.Where(t => t.Type == "Deposit").ToList();
        var withdraws = box.Transactions.Where(t => t.Type == "Withdraw").ToList();

        var totalDeposit = deposits.Sum(t => t.Amount * (t.ExchangeRate ?? 1m));
        var totalWithdraw = withdraws.Sum(t => t.Amount * (t.ExchangeRate ?? 1m));

        // Per-currency breakdown (raw amounts, not converted)
        var currencies = box.Transactions
            .Select(t => t.TxCurrency ?? box.Currency)
            .Distinct()
            .Where(c => c != null);

        var breakdowns = currencies.Select(c => new CurrencyBreakdown(
            c,
            CurrencySymbol(c),
            deposits.Where(t => (t.TxCurrency ?? box.Currency) == c).Sum(t => t.Amount),
            withdraws.Where(t => (t.TxCurrency ?? box.Currency) == c).Sum(t => t.Amount)
        )).Where(b => b.TotalDeposit > 0 || b.TotalWithdraw > 0).ToList();

        return new PaymentBoxSummaryResponse(
            box.Id,
            box.CompanyPresetId,
            box.CompanyPreset?.Name ?? "",
            box.Name,
            box.Currency,
            totalDeposit,
            totalWithdraw,
            totalDeposit - totalWithdraw,
            breakdowns,
            box.BankName,
            box.BankAddress,
            box.AccountNumber,
            box.BeneficiaryName,
            box.SwiftCode);
    }

    private static string CurrencySymbol(string currency) => currency switch
    {
        "USD" => "$",
        "EUR" => "€",
        "GBP" => "£",
        "CNY" => "¥",
        "AED" => "د.إ",
        "RUB" => "₽",
        _ => currency
    };
}
