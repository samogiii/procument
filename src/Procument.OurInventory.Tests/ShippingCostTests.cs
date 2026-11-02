using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Procument.Data;
using Procument.Module.Purchasing.Services;
using Procument.Module.Sales.Controllers;
using Procument.Module.Sales.Entities;
using Xunit;

namespace Procument.OurInventory.Tests;

/// <summary>Payment page, Shipping Costs tab: accepting withdraws from the chosen wallet, rejecting never does.</summary>
[Collection(StockDatabaseCollection.Name)]
public sealed class ShippingCostTests(StockDatabaseFixture fx)
{
    [SkippableFact]
    public async Task Accepting_withdraws_the_exact_yuan_total_from_a_cny_wallet()
    {
        fx.RequireDatabase();
        var wallet = await SeedWalletAsync("CNY");
        var costId = await SeedCostAsync(fixedCost: 285m, otherCost: 120m);

        await using var scope = fx.Scope();
        Assert.IsType<OkObjectResult>(await Controller(scope).Accept(costId, new AcceptShippingCostRequest(wallet, null)));

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ChangeTracker.Clear();
        var cost = await db.WarehouseShippingCosts.SingleAsync(c => c.Id == costId);
        var tx = await db.PaymentTransactions.SingleAsync(t => t.PaymentBoxId == wallet);
        Assert.Equal(WarehouseShippingCost.Accepted, cost.Status);
        Assert.Equal((wallet, 405m, tx.Id, (decimal?)null), (cost.PaymentBoxId!.Value, cost.WalletAmount!.Value, cost.PaymentTransactionId!.Value, cost.ExchangeRate));
        Assert.Equal(("Withdraw", "BankFee", "CNY", 405m), (tx.Type, tx.ToType, tx.TxCurrency, tx.Amount));
        Assert.Null(tx.ExchangeRate);
        Assert.True(tx.IsAuto);
        Assert.Contains("fixed 285.00 + others 120.00 CNY", tx.Notes);
    }

    [SkippableFact]
    public async Task A_dollar_wallet_needs_the_rate_and_is_debited_yuan_divided_by_it()
    {
        fx.RequireDatabase();
        var wallet = await SeedWalletAsync("USD");
        var costId = await SeedCostAsync(fixedCost: 285m, otherCost: 0m);

        await using var scope = fx.Scope();
        var controller = Controller(scope);
        Assert.IsType<BadRequestObjectResult>(await controller.Accept(costId, new AcceptShippingCostRequest(wallet, null)));
        Assert.IsType<OkObjectResult>(await controller.Accept(costId, new AcceptShippingCostRequest(wallet, 6.71m)));

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ChangeTracker.Clear();
        var tx = await db.PaymentTransactions.SingleAsync(t => t.PaymentBoxId == wallet);
        Assert.Equal(("USD", 42.47m), (tx.TxCurrency, tx.Amount));   // 285 ÷ 6.71
        Assert.Contains("at 6.71 CNY per 1 USD", tx.Notes);
        Assert.Equal(6.71m, (await db.WarehouseShippingCosts.SingleAsync(c => c.Id == costId)).ExchangeRate);
    }

    [SkippableFact]
    public async Task A_cost_is_withdrawn_only_once()
    {
        fx.RequireDatabase();
        var wallet = await SeedWalletAsync("CNY");
        var costId = await SeedCostAsync(fixedCost: 285m, otherCost: 0m);

        await using var scope = fx.Scope();
        var controller = Controller(scope);
        Assert.IsType<OkObjectResult>(await controller.Accept(costId, new AcceptShippingCostRequest(wallet, null)));
        Assert.IsType<BadRequestObjectResult>(await controller.Accept(costId, new AcceptShippingCostRequest(wallet, null)));
        Assert.IsType<BadRequestObjectResult>(await controller.Reject(costId, new RejectShippingCostRequest("Too late")));

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ChangeTracker.Clear();
        Assert.Equal(1, await db.PaymentTransactions.CountAsync(t => t.PaymentBoxId == wallet));
        Assert.Equal(WarehouseShippingCost.Accepted, (await db.WarehouseShippingCosts.SingleAsync(c => c.Id == costId)).Status);
    }

    [SkippableFact]
    public async Task Rejecting_needs_a_reason_and_withdraws_nothing()
    {
        fx.RequireDatabase();
        var wallet = await SeedWalletAsync("CNY");
        var costId = await SeedCostAsync(fixedCost: 285m, otherCost: 50m);

        await using var scope = fx.Scope();
        var controller = Controller(scope);
        Assert.IsType<BadRequestObjectResult>(await controller.Reject(costId, new RejectShippingCostRequest("  ")));
        Assert.IsType<OkObjectResult>(await controller.Reject(costId, new RejectShippingCostRequest("Others looks wrong")));
        Assert.IsType<BadRequestObjectResult>(await controller.Accept(costId, new AcceptShippingCostRequest(wallet, null)));

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ChangeTracker.Clear();
        var cost = await db.WarehouseShippingCosts.SingleAsync(c => c.Id == costId);
        Assert.Equal((WarehouseShippingCost.Rejected, "Others looks wrong"), (cost.Status, cost.RejectNote));
        Assert.False(await db.PaymentTransactions.AnyAsync(t => t.PaymentBoxId == wallet));
    }

    [SkippableFact]
    public async Task List_puts_waiting_costs_first_and_names_the_track_po_and_part()
    {
        fx.RequireDatabase();
        var part = await fx.NewPartAsync();
        var po = await fx.CreateApprovedStockPoAsync(fx.MainWarehouseId, (part.Id, 1, 10m, "NE"));
        var track = await fx.NewTrackAsync(po.Items[0].Id, fx.MainWarehouseId);
        var costId = await SeedCostAsync(fixedCost: 285m, otherCost: 0m, trackIds: track.ToString());

        await using var scope = fx.Scope();
        var result = Assert.IsType<OkObjectResult>(await Controller(scope).List());
        // As the browser reads it.
        var rows = JsonSerializer.SerializeToElement(result.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web)).EnumerateArray().ToList();

        Assert.Equal("Pending", rows[0].GetProperty("status").GetString());
        var row = rows.Single(r => r.GetProperty("id").GetInt64() == costId);
        Assert.Equal(po.PONumber, row.GetProperty("pos").EnumerateArray().Single().GetProperty("number").GetString());
        Assert.Equal(part.Name, row.GetProperty("parts").EnumerateArray().Single().GetString());
        Assert.Equal(285m, row.GetProperty("total").GetDecimal());
    }

    private static ShippingCostsController Controller(AsyncServiceScope scope) => new(
        scope.ServiceProvider.GetRequiredService<DbContext>(),
        scope.ServiceProvider.GetRequiredService<IShippingService>())
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
    };

    private async Task<long> SeedWalletAsync(string currency)
    {
        await using var scope = fx.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var box = new PaymentBox { Name = $"Wallet-{Guid.NewGuid():N}", CompanyPresetId = fx.PresetId, Currency = currency };
        db.PaymentBoxes.Add(box);
        await db.SaveChangesAsync();
        return box.Id;
    }

    private async Task<long> SeedCostAsync(decimal fixedCost, decimal otherCost, string trackIds = "")
    {
        await using var scope = fx.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cost = new WarehouseShippingCost
        {
            WarehouseShipmentId = Random.Shared.NextInt64(1, long.MaxValue),
            Version = 1, TrackNumber = $"TRK-{Guid.NewGuid():N}"[..14], TrackIds = trackIds,
            FixedCost = fixedCost, OtherCost = otherCost, EnteredBy = "Phone", EnteredAt = DateTime.UtcNow,
        };
        db.WarehouseShippingCosts.Add(cost);
        await db.SaveChangesAsync();
        return cost.Id;
    }
}
