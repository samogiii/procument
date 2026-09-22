using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Procument.Data;
using Procument.Module.OurInventory.DTOs;
using Procument.Module.OurInventory.Services;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Services;
using Xunit;

namespace Procument.OurInventory.Tests;

/// <summary>Epic 5: Stock PO quantities reach the ledger through accepted tracks and manual receipts.</summary>
[Collection(StockDatabaseCollection.Name)]
public sealed class ReceivingTests(StockDatabaseFixture fx)
{
    [SkippableFact]
    public async Task Accepted_track_books_one_receipt_and_recount_or_rejection_reconciles()
    {
        fx.RequireDatabase();
        var part = await fx.NewPartAsync();
        var po = await fx.CreateApprovedStockPoAsync(fx.MainWarehouseId, (part.Id, 5, 10m, "NE"));
        var line = po.Items.Single();
        var trackId = await fx.NewTrackAsync(line.Id, fx.MainWarehouseId);

        long itemId;
        await using (var s = fx.Scope())
        {
            var items = await s.ServiceProvider.GetRequiredService<IShippingService>().SubmitItemsAsync(trackId, fx.UserId,
                new SubmitTrackNumberItemsRequest { Items = [new() { POItemId = line.Id, ExpectedQty = 5, ActualQty = 4, IsAvailable = true }] });
            itemId = items.Single().Id;
        }
        Assert.Equal(0, (await fx.LotAsync(part.Id, fx.MainWarehouseId)).OnHand);

        await Review(trackId, itemId, "Accept");
        var lot = await fx.LotAsync(part.Id, fx.MainWarehouseId);
        Assert.Equal(4, lot.OnHand);
        Assert.Equal(10, lot.Avg);

        await Review(trackId, itemId, "Accept");
        Assert.Equal(4, (await fx.LotAsync(part.Id, fx.MainWarehouseId)).OnHand);

        await using (var s = fx.Scope())
            await s.ServiceProvider.GetRequiredService<IShippingService>().UpdateItemAsync(trackId, itemId, fx.UserId, new UpdateTrackNumberItemRequest { ActualQty = 5 });
        Assert.Equal(5, (await fx.LotAsync(part.Id, fx.MainWarehouseId)).OnHand);
        Assert.Equal("Completed", await PoStatus(po.Id));

        await Review(trackId, itemId, "Reject");
        Assert.Equal(0, (await fx.LotAsync(part.Id, fx.MainWarehouseId)).OnHand);

        await using var check = fx.Scope();
        var db = check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.OurStockMovements.CountAsync(m => m.TrackNumberId == trackId && m.Type == "Receipt"));
    }

    [SkippableFact]
    public async Task Manual_receipt_blocks_over_receipt_checks_serials_and_averages_cost()
    {
        fx.RequireDatabase();
        var part = await fx.NewPartAsync();
        var other = await fx.NewPartAsync();
        var po = await fx.CreateApprovedStockPoAsync(fx.MainWarehouseId, (part.Id, 5, 10m, "NE"), (other.Id, 2, 7m, "OH"), (part.Id, 5, 20m, "NE"));
        var lines = po.Items.OrderBy(i => i.PORef).ToList();

        await using var s = fx.Scope();
        var receipts = s.ServiceProvider.GetRequiredService<IStockReceiptService>();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => receipts.ReceiveManualAsync(po.Id,
            new ManualStockReceiptRequest { Lines = [new() { POItemId = lines[1].Id, Qty = 2, Serials = ["A"] }] }, fx.UserId));
        Assert.Contains("serial", ex.Message);

        var summary = await receipts.ReceiveManualAsync(po.Id, new ManualStockReceiptRequest
        {
            Lines = [
                new() { POItemId = lines[0].Id, Qty = 5 },
                new() { POItemId = lines[1].Id, Qty = 2, Serials = [$"S1-{part.Id}", $"S2-{part.Id}"] },
                new() { POItemId = lines[2].Id, Qty = 5 },
            ],
        }, fx.UserId);
        Assert.True(summary!.FullyReceived);
        Assert.Equal(2, summary.Lines.Single(l => l.POItemId == lines[1].Id).Movements.Single().Serials.Count);
        Assert.Equal(15, (await fx.LotAsync(part.Id, fx.MainWarehouseId)).Avg);
        Assert.Equal("Completed", await PoStatus(po.Id));

        ex = await Assert.ThrowsAsync<InvalidOperationException>(() => receipts.ReceiveManualAsync(po.Id,
            new ManualStockReceiptRequest { Lines = [new() { POItemId = lines[0].Id, Qty = 1 }] }, fx.UserId));
        Assert.Contains("still to be received", ex.Message);
    }

    [SkippableFact]
    public async Task Stock_po_create_and_update_work_under_the_retrying_strategy()
    {
        fx.RequireDatabase();
        var part = await fx.NewPartAsync();
        await using var s = fx.Scope();
        var service = s.ServiceProvider.GetRequiredService<IStockPurchaseOrderService>();
        var request = new SaveStockPurchaseOrderRequest
        {
            SupplierId = fx.SupplierId, CompanyPresetId = fx.PresetId, DestinationWarehouseId = fx.MainWarehouseId,
            Items = [new() { PartNumberId = part.Id, Qty = 2, UnitPrice = 5 }],
        };
        var created = await service.CreateAsync(request);
        Assert.Equal($"SPO-{created.Id}", created.PONumber);
        request.Items[0].Qty = 4;
        Assert.Equal(20, (await service.UpdateAsync(created.Id, request))!.TotalAmount);
    }

    private async Task Review(long trackId, long itemId, string action)
    {
        await using var s = fx.Scope();
        await s.ServiceProvider.GetRequiredService<IShippingService>().ReviewItemAsync(trackId, itemId, fx.UserId, new ReviewTrackNumberItemRequest { Action = action });
    }

    private async Task<string> PoStatus(long poId)
    {
        await using var s = fx.Scope();
        return (await s.ServiceProvider.GetRequiredService<AppDbContext>().PurchaseOrders.AsNoTracking().FirstAsync(p => p.Id == poId)).Status;
    }
}
