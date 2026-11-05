using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Procument.Data;
using Procument.Module.Sales.DTOs;
using Procument.Module.Sales.Entities;
using Procument.Module.Sales.Services;
using Xunit;

namespace Procument.OurInventory.Tests;

/// <summary>
/// Wallet transfers: the warehouse POP puts them in Review, the office accepts the numbers (handling, bank fee and our
/// part of the wire fee off the source, their part off what arrives). The rate is per 1 USD whichever way the money goes.
/// </summary>
[Collection(StockDatabaseCollection.Name)]
public sealed class WalletTransferTests(StockDatabaseFixture fx)
{
    [Theory]
    [InlineData("CNY", "USD", "USD")]
    [InlineData("USD", "CNY", "USD")]
    [InlineData("CNY", "HKD", "CNY")]
    [InlineData("CNY", "CNY", "CNY")]
    public void Rate_is_counted_per_dollar_when_a_dollar_wallet_is_involved(string from, string to, string expectedBase)
        => Assert.Equal(expectedBase, WalletTransferPending.RateBase(from, to));

    [Fact]
    public void Conversion_divides_into_dollars_and_multiplies_out_of_them()
    {
        Assert.Equal(144.72m, WalletTransferPending.Convert(1000m, "CNY", "USD", 6.91m));
        Assert.Equal(6910m, WalletTransferPending.Convert(1000m, "USD", "CNY", 6.91m));
        Assert.Equal(500m, WalletTransferPending.Convert(500m, "CNY", "CNY", null));
    }

    [SkippableFact]
    public async Task Pop_waits_in_review_then_accept_takes_handling_off_the_source()
    {
        fx.RequireDatabase();
        var (a, b) = await NewWalletsAsync("CNY", "CNY");
        var id = await CreateAsync(a, b, 500m);

        await using var s = fx.Scope();
        var service = s.ServiceProvider.GetRequiredService<IWalletTransferService>();
        Assert.Null(await service.StoreFromWarehouseAsync(id, null, null, null, "Phone", DateTime.UtcNow, "pop.jpg", [1, 2, 3], StorageRoot()));

        Assert.Equal(0m, await BalanceAsync(a));      // nothing moves until the office accepts
        var waiting = await service.GetByIdAsync(id);
        Assert.Equal(WalletTransferService.Review, waiting!.Status);
        Assert.Equal(PopReading.Waiting, waiting.Reading?.Status);

        Assert.Null(await service.AcceptAsync(id, new AcceptWalletTransferRequest(500m, null, 200m, 0m, 0m, 0m), fx.UserId));
        Assert.Equal(-700m, await BalanceAsync(a));   // bank A: −500 −200 handling
        Assert.Equal(500m, await BalanceAsync(b));    // yuan transfers have no wire fee
    }

    [SkippableFact]
    public async Task Exchange_transfer_uses_the_warehouse_rate_and_splits_the_wire_fee()
    {
        fx.RequireDatabase();
        var (a, b) = await NewWalletsAsync("USD", "CNY");
        var id = await CreateAsync(a, b, 1000m);

        await using var s = fx.Scope();
        var service = s.ServiceProvider.GetRequiredService<IWalletTransferService>();
        Assert.Null(await service.StoreFromWarehouseAsync(id, 7.11m, 6.91m, "2026.11.04 10:30:00", "Phone", DateTime.UtcNow, "pop.jpg", [1], StorageRoot()));
        var waiting = (await service.GetByIdAsync(id))!;
        Assert.Equal(7.11m, waiting.ExchangeRate);
        Assert.Equal("2026.11.04 10:30:00", waiting.RateAt);

        Assert.Null(await service.AcceptAsync(id, new AcceptWalletTransferRequest(1000m, 7.11m, 10m, 0m, 15m, 106.65m), fx.UserId));
        Assert.Equal(-1025m, await BalanceAsync(a));     // 1000 + handling 10 + our half of the wire fee 15
        Assert.Equal(7003.35m, await BalanceAsync(b));   // 7110 − their half (15 × 7.11)
        // A resend after a lost ack changes nothing.
        Assert.Null(await service.StoreFromWarehouseAsync(id, 7.11m, 6.91m, null, "Phone", DateTime.UtcNow, "pop.jpg", [1], StorageRoot()));
        Assert.Equal(-1025m, await BalanceAsync(a));
    }

    [SkippableFact]
    public async Task Destination_wire_fee_above_what_arrives_is_refused()
    {
        fx.RequireDatabase();
        var (a, b) = await NewWalletsAsync("CNY", "USD");
        var id = await CreateAsync(a, b, 100m);

        await using var s = fx.Scope();
        var service = s.ServiceProvider.GetRequiredService<IWalletTransferService>();
        Assert.Null(await service.StoreFromWarehouseAsync(id, 6.91m, null, null, "Phone", DateTime.UtcNow, "pop.jpg", [1], StorageRoot()));
        Assert.NotNull(await service.AcceptAsync(id, new AcceptWalletTransferRequest(100m, 6.91m, 0m, 0m, 0m, 20m), fx.UserId));
        Assert.Equal(0m, await BalanceAsync(a));
    }

    private static string StorageRoot() => Path.Combine(Path.GetTempPath(), "procument-transfer-tests");

    private async Task<(long A, long B)> NewWalletsAsync(string fromCurrency, string toCurrency)
    {
        await using var s = fx.Scope();
        var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
        var a = new PaymentBox { Name = $"A-{Guid.NewGuid():N}"[..10], Currency = fromCurrency, CompanyPresetId = fx.PresetId };
        var b = new PaymentBox { Name = $"B-{Guid.NewGuid():N}"[..10], Currency = toCurrency, CompanyPresetId = fx.PresetId };
        db.AddRange(a, b);
        await db.SaveChangesAsync();
        return (a.Id, b.Id);
    }

    private async Task<long> CreateAsync(long from, long to, decimal amount)
    {
        await using var s = fx.Scope();
        var created = await s.ServiceProvider.GetRequiredService<IWalletTransferService>()
            .CreateAsync(new CreateWalletTransferPendingRequest(from, to, amount, 0m, null, null), fx.UserId);
        return created.Id;
    }

    /// <summary>Movement on the wallet from the transfer rows (amounts are in the wallet's currency).</summary>
    private async Task<decimal> BalanceAsync(long boxId)
    {
        await using var s = fx.Scope();
        var rows = await s.ServiceProvider.GetRequiredService<AppDbContext>().Set<PaymentTransaction>().AsNoTracking()
            .Where(t => t.PaymentBoxId == boxId)
            .ToListAsync();
        return rows.Sum(t => t.Type == "Deposit" ? t.Amount : -t.Amount);
    }
}
