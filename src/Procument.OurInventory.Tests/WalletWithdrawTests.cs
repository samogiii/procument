using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Procument.Data;
using Procument.Module.Sales.DTOs;
using Procument.Module.Sales.Entities;
using Procument.Module.Sales.Services;
using Xunit;

namespace Procument.OurInventory.Tests;

/// <summary>
/// Withdraws paid by the warehouse: nothing moves until the POP arrives and the office accepts; then the wallet loses
/// amount × rate (the withdraw row) + handling, bank and wire fees (Bank Fee rows).
/// </summary>
[Collection(StockDatabaseCollection.Name)]
public sealed class WalletWithdrawTests(StockDatabaseFixture fx)
{
    private static string StorageRoot() => Path.Combine(Path.GetTempPath(), "procument-withdraw-tests");

    [SkippableFact]
    public async Task Dollar_withdraw_from_a_yuan_wallet_takes_amount_times_rate_plus_fees_on_accept()
    {
        fx.RequireDatabase();
        var wallet = await NewWalletAsync("CNY");
        await using var s = fx.Scope();
        var service = s.ServiceProvider.GetRequiredService<IWalletWithdrawService>();
        var created = await service.CreateAsync(new CreateWalletWithdrawRequest(wallet, 1000m, "USD", 6.9m, "MotherWallet", null, null, null, "Rent"), fx.UserId);
        Assert.Equal(WireFeePayer.Ours, created.WireFeePaidBy);   // wallet setting not set → our wallet pays, as before

        Assert.Null(await service.StoreFromWarehouseAsync(created.Id, 6.91m, 6.71m, "2026.11.04 10:30:00", "Phone", DateTime.UtcNow, "pop.jpg", [1, 2], StorageRoot()));
        Assert.Equal(0m, await MovementAsync(wallet));   // in Review: nothing moved
        var review = (await service.GetAllAsync()).Single(w => w.Id == created.Id);
        Assert.Equal(WalletWithdrawPending.Review, review.Status);
        Assert.Equal(6.91m, review.ExchangeRate);
        Assert.NotNull(review.Proposal);

        Assert.Null(await service.AcceptAsync(created.Id, new AcceptWalletWithdrawRequest(1000m, 6.91m, 200m, 30m, 0m)));
        Assert.Equal(-(6910m + 230m), await MovementAsync(wallet));
        var done = (await service.GetAllAsync()).Single(w => w.Id == created.Id);
        Assert.Equal("Completed", done.Status);
        Assert.Equal(6910m, done.WalletAmount);
        // A resend after a lost ack changes nothing.
        Assert.Null(await service.StoreFromWarehouseAsync(created.Id, 6.91m, null, null, "Phone", DateTime.UtcNow, "pop.jpg", [1], StorageRoot()));
        Assert.Equal(-7140m, await MovementAsync(wallet));
    }

    [SkippableFact]
    public async Task Missing_rate_is_refused_and_a_pending_withdraw_can_be_deleted()
    {
        fx.RequireDatabase();
        var wallet = await NewWalletAsync("CNY");
        await using var s = fx.Scope();
        var service = s.ServiceProvider.GetRequiredService<IWalletWithdrawService>();
        var created = await service.CreateAsync(new CreateWalletWithdrawRequest(wallet, 100m, "USD", null, "MotherWallet", null, null, null, null), fx.UserId);
        Assert.True(await service.DeleteAsync(created.Id));

        var other = await service.CreateAsync(new CreateWalletWithdrawRequest(wallet, 100m, "USD", null, "MotherWallet", null, null, null, null), fx.UserId);
        Assert.Null(await service.StoreFromWarehouseAsync(other.Id, null, null, null, "Phone", DateTime.UtcNow, "pop.jpg", [1], StorageRoot()));
        Assert.NotNull(await service.AcceptAsync(other.Id, new AcceptWalletWithdrawRequest(100m, null, 0m, 0m, 0m)));
        Assert.Equal(0m, await MovementAsync(wallet));
        Assert.False(await service.DeleteAsync(other.Id));   // the POP is in: no longer deletable
    }

    private async Task<long> NewWalletAsync(string currency)
    {
        await using var s = fx.Scope();
        var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
        var box = new PaymentBox { Name = $"W-{Guid.NewGuid():N}"[..10], Currency = currency, CompanyPresetId = fx.PresetId };
        db.Add(box);
        await db.SaveChangesAsync();
        return box.Id;
    }

    /// <summary>Movement in the wallet currency (Amount × ExchangeRate when the row is in another currency).</summary>
    private async Task<decimal> MovementAsync(long boxId)
    {
        await using var s = fx.Scope();
        var rows = await s.ServiceProvider.GetRequiredService<AppDbContext>().Set<PaymentTransaction>().AsNoTracking()
            .Where(t => t.PaymentBoxId == boxId)
            .ToListAsync();
        return rows.Sum(t => (t.Type == "Deposit" ? 1 : -1) * t.Amount * (t.ExchangeRate ?? 1m));
    }
}
