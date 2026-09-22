using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Procument.Data;
using Procument.Module.OurInventory.DTOs;
using Procument.Module.OurInventory.Services;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Services;
using Xunit;

namespace Procument.OurInventory.Tests;

/// <summary>Epic 6: opening stock, adjust, manual and physical transfers, lot details.</summary>
[Collection(StockDatabaseCollection.Name)]
public sealed class StockManagementTests(StockDatabaseFixture fx)
{
    [SkippableFact]
    public async Task Opening_import_validates_every_row_and_saves_nothing_on_error()
    {
        fx.RequireDatabase();
        var known = await fx.NewPartAsync();
        var newName = $"NEW-{Guid.NewGuid():N}"[..14];
        await using var s = fx.Scope();
        var manage = s.ServiceProvider.GetRequiredService<IStockManagementService>();

        var bad = await manage.ImportOpeningAsync(new OpeningStockRequest
        {
            Lines = [
                new() { PartNumber = known.Name, Warehouse = "Main", CompanyPreset = "Test Company", Qty = 4, UnitCost = 10 },
                new() { PartNumber = newName, Warehouse = "Nowhere", CompanyPreset = "Test Company", Qty = 1, UnitCost = 1 },
                new() { PartNumber = newName, Warehouse = "Main", CompanyPreset = "Test Company", Qty = 2, UnitCost = 1, Serials = ["only-one"] },
            ],
        }, fx.UserId);
        Assert.False(bad.Saved);
        Assert.Equal([2, 3], bad.Errors.Select(e => e.Row));
        Assert.Equal(0, (await fx.LotAsync(known.Id, fx.MainWarehouseId)).OnHand);

        var dry = await manage.ImportOpeningAsync(new OpeningStockRequest
        {
            DryRun = true,
            Lines = [new() { PartNumber = newName, Warehouse = "main", CompanyPreset = "test company", Qty = 2, UnitCost = 3 }],
        }, fx.UserId);
        Assert.False(dry.Saved);
        Assert.Empty(dry.Errors);
        Assert.Equal(1, dry.NewPartNumbers);

        var ok = await manage.ImportOpeningAsync(new OpeningStockRequest
        {
            Reference = "Opening test",
            Lines = [
                new() { PartNumber = known.Name, Warehouse = "Main", CompanyPreset = "Test Company", Qty = 4, UnitCost = 10, BinLocation = "B-1", MinQty = 5 },
                new() { PartNumber = newName, Warehouse = "Second", CompanyPreset = "Test Company", Qty = 2, UnitCost = 3, Condition = "oh", Serials = [$"{newName}-1", $"{newName}-2"] },
            ],
        }, fx.UserId);
        Assert.True(ok.Saved);
        Assert.Equal(2, ok.LotsTouched);
        Assert.Equal(1, ok.NewPartNumbers);

        var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
        var lot = await db.OurStockItems.AsNoTracking().FirstAsync(l => l.PartNumberId == known.Id);
        Assert.Equal((4m, "B-1", 5m), (lot.QtyOnHand, lot.BinLocation, lot.MinQty!.Value));
        var created = await db.OurStockItems.AsNoTracking().Include(l => l.PartNumber).FirstAsync(l => l.PartNumber.Name == newName);
        Assert.Equal("OH", created.Condition);
        Assert.Equal(2, await db.OurStockSerials.CountAsync(x => x.StockItemId == created.Id));
    }

    [SkippableFact]
    public async Task Adjust_needs_a_reason_and_manual_transfer_keeps_cost()
    {
        fx.RequireDatabase();
        var part = await fx.NewPartAsync();
        await using var s = fx.Scope();
        var manage = s.ServiceProvider.GetRequiredService<IStockManagementService>();
        await manage.ImportOpeningAsync(new OpeningStockRequest
        {
            Lines = [new() { PartNumberId = part.Id, WarehouseId = fx.MainWarehouseId, CompanyPresetId = fx.PresetId, Qty = 10, UnitCost = 12 }],
        }, fx.UserId);
        var lotId = await fx.LotIdAsync(part.Id, fx.MainWarehouseId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => manage.AdjustAsync(lotId, new StockAdjustRequest { Qty = -1, Reason = "" }, fx.UserId));
        await manage.AdjustAsync(lotId, new StockAdjustRequest { Qty = -2, Reason = "Damaged in store" }, fx.UserId);
        Assert.Equal(8, (await fx.LotAsync(part.Id, fx.MainWarehouseId)).OnHand);

        await manage.TransferAsync(lotId, new StockTransferRequest { TargetWarehouseId = fx.SecondWarehouseId, Qty = 3 }, fx.UserId);
        Assert.Equal(5, (await fx.LotAsync(part.Id, fx.MainWarehouseId)).OnHand);
        var moved = await fx.LotAsync(part.Id, fx.SecondWarehouseId);
        Assert.Equal((3m, 12m), (moved.OnHand, moved.Avg));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manage.TransferAsync(lotId, new StockTransferRequest { TargetWarehouseId = fx.SecondWarehouseId, Qty = 50 }, fx.UserId));
        Assert.Contains("available", ex.Message);

        Assert.True(await manage.UpdateAsync(lotId, new UpdateStockItemRequest { BinLocation = " C-9 ", MinQty = 2, Notes = "top shelf" }));
        var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
        var lot = await db.OurStockItems.AsNoTracking().FirstAsync(l => l.Id == lotId);
        Assert.Equal(("C-9", 2m, "top shelf"), (lot.BinLocation, lot.MinQty!.Value, lot.Notes));
    }

    [SkippableFact]
    public async Task Warehouse_transfer_leg_moves_stock_when_accepted_and_stock_items_skip_ready_for_sn()
    {
        fx.RequireDatabase();
        var part = await fx.NewPartAsync();
        var po = await fx.CreateApprovedStockPoAsync(fx.MainWarehouseId, (part.Id, 6, 9m, "NE"));
        var line = po.Items.Single();
        var sourceTrack = await fx.NewTrackAsync(line.Id, fx.MainWarehouseId);

        await using (var s = fx.Scope())
            await s.ServiceProvider.GetRequiredService<IShippingService>().ReceiveAndAcceptAllAsync(sourceTrack, fx.UserId);
        Assert.Equal(6, (await fx.LotAsync(part.Id, fx.MainWarehouseId)).OnHand);

        await using (var s = fx.Scope())
        {
            var ready = await s.ServiceProvider.GetRequiredService<IShippingService>().GetReadyForSnAsync(fx.MainWarehouseId);
            Assert.DoesNotContain(ready, r => r.POItemId == line.Id);
        }

        long destinationTrack;
        await using (var s = fx.Scope())
        {
            var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
            var sourceItemId = await db.Set<Procument.Module.Purchasing.Entities.TrackNumberItem>()
                .Where(i => i.TrackNumberId == sourceTrack).Select(i => i.Id).SingleAsync();
            var transfer = await s.ServiceProvider.GetRequiredService<IWarehouseTransferService>().CreateAsync(fx.UserId,
                new CreateWarehouseTransferRequest
                {
                    FromWarehouseId = fx.MainWarehouseId, ToWarehouseId = fx.SecondWarehouseId,
                    TrackNumber = $"WT-{Guid.NewGuid():N}"[..12], Items = [new() { SourceTrackNumberItemId = sourceItemId, Qty = 4 }],
                }, null);
            destinationTrack = transfer.DestinationTrackNumberIds.Single();
        }
        // In transit: still at the source.
        Assert.Equal(6, (await fx.LotAsync(part.Id, fx.MainWarehouseId)).OnHand);

        await using (var s = fx.Scope())
            await s.ServiceProvider.GetRequiredService<IShippingService>().ReceiveAndAcceptAllAsync(destinationTrack, fx.UserId);
        Assert.Equal(2, (await fx.LotAsync(part.Id, fx.MainWarehouseId)).OnHand);
        Assert.Equal(4, (await fx.LotAsync(part.Id, fx.SecondWarehouseId)).OnHand);

        // Accepting the same leg again moves nothing more.
        await using (var s = fx.Scope())
            await s.ServiceProvider.GetRequiredService<IShippingService>().ReceiveAndAcceptAllAsync(destinationTrack, fx.UserId);
        Assert.Equal(4, (await fx.LotAsync(part.Id, fx.SecondWarehouseId)).OnHand);
    }
}
