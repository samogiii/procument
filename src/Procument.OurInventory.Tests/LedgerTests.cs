using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Procument.Data;
using Procument.Module.OurInventory.DTOs;
using Procument.Module.OurInventory.Services;
using Procument.Shared.Services;
using Xunit;

namespace Procument.OurInventory.Tests;

/// <summary>Ledger rules: reservations, issue, adjust, transfer, concurrency and the on-hand invariant.</summary>
[Collection(StockDatabaseCollection.Name)]
public sealed class LedgerTests(StockDatabaseFixture fx)
{
    private async Task<(long PartId, long LotId)> StockedLotAsync(decimal qty, decimal cost)
    {
        var part = await fx.NewPartAsync();
        await using var s = fx.Scope();
        await s.ServiceProvider.GetRequiredService<IStockManagementService>().ImportOpeningAsync(new OpeningStockRequest
        {
            Lines = [new() { PartNumberId = part.Id, WarehouseId = fx.MainWarehouseId, CompanyPresetId = fx.PresetId, Condition = "NE", Qty = qty, UnitCost = cost }],
        }, fx.UserId);
        return (part.Id, await fx.LotIdAsync(part.Id, fx.MainWarehouseId));
    }

    [SkippableFact]
    public async Task Reservations_are_idempotent_partial_and_consumed_as_issues()
    {
        fx.RequireDatabase();
        var (partId, lotId) = await StockedLotAsync(10, 15);
        var soLine = Random.Shared.NextInt64(1_000_000, 9_000_000);

        await using var s = fx.Scope();
        var res = s.ServiceProvider.GetRequiredService<IStockReservationService>();
        Assert.Equal(7, (await res.ReserveAsync(lotId, 7, soLine, fx.UserId)).ReservedQuantity);
        Assert.Equal(7, (await res.ReserveAsync(lotId, 7, soLine, fx.UserId)).ReservedQuantity);
        Assert.Equal(7, (await fx.LotAsync(partId, fx.MainWarehouseId)).Reserved);

        var partial = await res.ReserveAsync(lotId, 10, soLine + 1, fx.UserId);
        Assert.Equal(3, partial.ReservedQuantity);
        Assert.Equal(7, partial.RemainingQuantity);

        var consumed = await res.ConsumeAsync(soLine, 3, fx.UserId);
        Assert.Equal(3, consumed.ReservedQuantity);
        var lot = await fx.LotAsync(partId, fx.MainWarehouseId);
        Assert.Equal(7, lot.OnHand);
        Assert.Equal(7, lot.Reserved);

        var ledger = s.ServiceProvider.GetRequiredService<IStockLedgerService>();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ledger.ExecuteAsync(() =>
            ledger.AdjustAsync(lotId, -1, new StockMovementContext { UserId = fx.UserId, Reference = "ADJ", Reason = "count" })));
        Assert.Contains("unreserved", ex.Message);

        await res.ReleaseAsync(soLine, fx.UserId);
        await res.ReleaseAsync(soLine + 1, fx.UserId);
        Assert.Equal(0, (await fx.LotAsync(partId, fx.MainWarehouseId)).Reserved);
    }

    [SkippableFact]
    public async Task Parallel_reservations_never_over_reserve()
    {
        fx.RequireDatabase();
        var (partId, lotId) = await StockedLotAsync(5, 1);
        var baseLine = Random.Shared.NextInt64(10_000_000, 90_000_000);
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(async i =>
        {
            await using var s = fx.Scope();
            return await s.ServiceProvider.GetRequiredService<IStockReservationService>().ReserveAsync(lotId, 2, baseLine + i, fx.UserId);
        }));
        Assert.Equal(5, results.Sum(r => r.ReservedQuantity));
        Assert.Equal(5, (await fx.LotAsync(partId, fx.MainWarehouseId)).Reserved);
    }

    [SkippableFact]
    public async Task Ledger_sum_always_matches_on_hand()
    {
        fx.RequireDatabase();
        await StockedLotAsync(3, 2);
        await using var s = fx.Scope();
        var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
        var drift = await db.OurStockItems.AsNoTracking()
            .Where(l => db.OurStockMovements.Where(m => m.StockItemId == l.Id).Sum(m => m.Qty) != l.QtyOnHand)
            .CountAsync();
        Assert.Equal(0, drift);
    }
}
