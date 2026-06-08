using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Identity.Entities;
using Procument.Module.Sales.DTOs;
using Procument.Module.Sales.Entities;

namespace Procument.Module.Sales.Services;

public class WalletTransferService : IWalletTransferService
{
    private readonly DbContext _db;

    public WalletTransferService(DbContext db) => _db = db;

    public async Task<List<WalletTransferPendingResponse>> GetAllAsync()
    {
        var transfers = await _db.Set<WalletTransferPending>()
            .Include(t => t.FromBox).ThenInclude(b => b.CompanyPreset)
            .Include(t => t.ToBox).ThenInclude(b => b.CompanyPreset)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        var userIds = transfers.Select(t => t.CreatedByUserId).Distinct().ToList();
        var users = await _db.Set<User>()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name);

        return transfers.Select(t => ToResponse(t, users)).ToList();
    }

    public async Task<WalletTransferPendingResponse?> GetByIdAsync(long id)
    {
        var t = await _db.Set<WalletTransferPending>()
            .Include(t => t.FromBox).ThenInclude(b => b.CompanyPreset)
            .Include(t => t.ToBox).ThenInclude(b => b.CompanyPreset)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (t == null) return null;
        var user = await _db.Set<User>().FindAsync(t.CreatedByUserId);
        var users = new Dictionary<long, string> { { t.CreatedByUserId, user?.Name ?? "Unknown" } };
        return ToResponse(t, users);
    }

    public async Task<WalletTransferPendingResponse> CreateAsync(CreateWalletTransferPendingRequest req, long userId)
    {
        var transfer = new WalletTransferPending
        {
            FromBoxId = req.FromBoxId,
            ToBoxId = req.ToBoxId,
            WithdrawAmount = req.WithdrawAmount,
            DepositAmount = req.DepositAmount,
            ExchangeRate = req.ExchangeRate,
            Notes = req.Notes,
            CreatedByUserId = userId,
        };
        _db.Set<WalletTransferPending>().Add(transfer);
        await _db.SaveChangesAsync();
        return (await GetByIdAsync(transfer.Id))!;
    }

    public async Task<bool> ReviewAsync(long id, string decision, string? note, long userId)
    {
        var transfer = await _db.Set<WalletTransferPending>().FindAsync(id);
        if (transfer == null) return false;

        if (decision == "Accept")
        {
            transfer.Status = "Accepted";
            transfer.AcceptedByUserId = userId;
            transfer.AcceptedAt = DateTime.UtcNow;
        }
        else
        {
            transfer.Status = "Rejected";
            transfer.RejectionNote = note;
        }
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UploadPopAndExecuteAsync(long id, IFormFile file, long userId, string storageRoot)
    {
        var transfer = await _db.Set<WalletTransferPending>()
            .Include(t => t.FromBox)
            .Include(t => t.ToBox)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (transfer == null || transfer.Status != "Accepted") return false;

        // Save POP file
        var dir = Path.Combine(storageRoot, "WalletTransfers", id.ToString(), "pop");
        Directory.CreateDirectory(dir);
        var ext = Path.GetExtension(file.FileName);
        var fileName = $"pop_{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";
        using (var stream = new FileStream(Path.Combine(dir, fileName), FileMode.Create))
            await file.CopyToAsync(stream);
        transfer.PopFileName = fileName;

        // Execute the wallet-to-wallet transfer atomically.
        // Amounts are already in each wallet's base currency — do NOT set ExchangeRate
        // on the transactions or the balance formula (Amount * ExchangeRate) would double-count.
        // Embed the rate in Notes for display only (same approach as PaymentBoxService.TransferAsync).
        var fromName = transfer.FromBox.CompanyPreset?.Name ?? $"Wallet {transfer.FromBoxId}";
        var toName   = transfer.ToBox.CompanyPreset?.Name   ?? $"Wallet {transfer.ToBoxId}";
        var rateNote = transfer.ExchangeRate.HasValue
            ? $" (Rate: 1 {transfer.FromBox.Currency} = {transfer.ExchangeRate} {transfer.ToBox.Currency})"
            : "";
        var userNote = transfer.Notes != null ? $" — {transfer.Notes}" : "";

        var fromTx = new PaymentTransaction
        {
            PaymentBoxId = transfer.FromBoxId,
            Type = "Withdraw",
            Amount = transfer.WithdrawAmount,
            FromType = "Wallet",
            ToType = "Wallet",
            ToPaymentBoxId = transfer.ToBoxId,
            Notes = $"Wallet Transfer → {toName}{rateNote}{userNote}",
            IsAuto = false,
            CreatedAt = DateTime.UtcNow,
        };
        var toTx = new PaymentTransaction
        {
            PaymentBoxId = transfer.ToBoxId,
            Type = "Deposit",
            Amount = transfer.DepositAmount,
            FromType = "Wallet",
            ToType = "Wallet",
            ToPaymentBoxId = transfer.FromBoxId,
            Notes = $"Wallet Transfer ← {fromName}{rateNote}{userNote}",
            IsAuto = false,
            CreatedAt = DateTime.UtcNow,
        };

        transfer.Status = "Completed";
        transfer.CompletedByUserId = userId;
        transfer.CompletedAt = DateTime.UtcNow;

        _db.Set<PaymentTransaction>().AddRange(fromTx, toTx);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<(Stream Stream, string FileName, string MimeType)?> GetPopFileAsync(long id, string storageRoot)
    {
        var transfer = await _db.Set<WalletTransferPending>().FindAsync(id);
        if (transfer?.PopFileName == null) return null;
        var filePath = Path.Combine(storageRoot, "WalletTransfers", id.ToString(), "pop", transfer.PopFileName);
        if (!File.Exists(filePath)) return null;
        var mime = transfer.PopFileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            ? "application/pdf"
            : "application/octet-stream";
        return (File.OpenRead(filePath), transfer.PopFileName, mime);
    }

    private static WalletTransferPendingResponse ToResponse(WalletTransferPending t, Dictionary<long, string> users) =>
        new(t.Id,
            t.FromBoxId, t.FromBox.CompanyPreset.Name, t.FromBox.Currency,
            t.ToBoxId, t.ToBox.CompanyPreset.Name, t.ToBox.Currency,
            t.WithdrawAmount, t.DepositAmount, t.ExchangeRate,
            t.Notes, t.Status, t.PopFileName, t.RejectionNote,
            t.CreatedByUserId, users.GetValueOrDefault(t.CreatedByUserId, "Unknown"),
            t.CreatedAt, t.AcceptedAt, t.CompletedAt);
}
