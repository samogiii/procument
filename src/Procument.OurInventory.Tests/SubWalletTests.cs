using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Procument.Data;
using Procument.Module.Catalog.Entities;
using Procument.Module.Sales.DTOs;
using Procument.Module.Sales.Entities;
using Procument.Module.Sales.Services;
using Xunit;

namespace Procument.OurInventory.Tests;

[Collection(StockDatabaseCollection.Name)]
public sealed class SubWalletTests(StockDatabaseFixture fx)
{
    [SkippableFact]
    public async Task Moving_a_wallet_under_a_main_wallet_keeps_every_transaction_and_balance()
    {
        fx.RequireDatabase();
        var main = await SeedWalletAsync("USD", deposit: 100m, withdraw: 30m);
        var sub = await SeedWalletAsync("CNY", deposit: 700m, withdraw: 70m);

        await using var scope = fx.Scope();
        var service = scope.ServiceProvider.GetRequiredService<IPaymentBoxService>();
        var before = (await service.GetByIdAsync(sub))!.Transactions.Select(t => t.Id).ToList();

        await service.RenameBoxAsync(sub, new RenamePaymentBoxRequest("Yuan", fx.PresetId, main));

        var boxes = await service.GetAllAsync();
        var mainRow = boxes.Single(b => b.Id == main);
        var subRow = boxes.Single(b => b.Id == sub);

        // Each wallet's own figures are untouched; only the group view adds them up.
        Assert.Equal(70m, mainRow.Balance);
        Assert.Equal(630m, subRow.Balance);
        Assert.Equal(90m, subRow.BalanceInParentCurrency);
        Assert.Equal(1, mainRow.Group!.SubWalletCount);
        Assert.Equal(200m, mainRow.Group.TotalDeposit);
        Assert.Equal(40m, mainRow.Group.TotalWithdraw);
        Assert.Equal(160m, mainRow.Group.Balance);
        Assert.Empty(mainRow.Group.NotConverted);
        Assert.Equal(before, (await service.GetByIdAsync(sub))!.Transactions.Select(t => t.Id).ToList());
    }

    [SkippableFact]
    public async Task Moving_a_wallet_back_out_restores_a_plain_main_wallet()
    {
        fx.RequireDatabase();
        var main = await SeedWalletAsync("USD", deposit: 100m, withdraw: 0m);
        var sub = await SeedWalletAsync("USD", deposit: 50m, withdraw: 0m);

        await using var scope = fx.Scope();
        var service = scope.ServiceProvider.GetRequiredService<IPaymentBoxService>();
        await service.RenameBoxAsync(sub, new RenamePaymentBoxRequest("Sub", fx.PresetId, main));
        await service.RenameBoxAsync(sub, new RenamePaymentBoxRequest("Sub", fx.PresetId));

        var boxes = await service.GetAllAsync();
        Assert.Null(boxes.Single(b => b.Id == main).Group);
        Assert.Null(boxes.Single(b => b.Id == sub).ParentBoxId);
        Assert.Equal(50m, boxes.Single(b => b.Id == sub).Balance);
    }

    [SkippableFact]
    public async Task A_currency_with_no_rate_is_reported_apart_from_the_total()
    {
        fx.RequireDatabase();
        var main = await SeedWalletAsync("USD", deposit: 100m, withdraw: 0m);
        var sub = await SeedWalletAsync("AED", deposit: 500m, withdraw: 0m);

        await using var scope = fx.Scope();
        var service = scope.ServiceProvider.GetRequiredService<IPaymentBoxService>();
        await service.RenameBoxAsync(sub, new RenamePaymentBoxRequest("Dirham", fx.PresetId, main));

        var boxes = await service.GetAllAsync();
        var group = boxes.Single(b => b.Id == main).Group!;
        Assert.Equal(100m, group.Balance);
        Assert.Equal(("AED", 500m), (group.NotConverted.Single().Currency, group.NotConverted.Single().Balance));
        Assert.Null(boxes.Single(b => b.Id == sub).BalanceInParentCurrency);
    }

    [SkippableFact]
    public async Task Links_that_would_nest_or_loop_are_refused()
    {
        fx.RequireDatabase();
        var main = await SeedWalletAsync("USD", deposit: 0m, withdraw: 0m);
        var sub = await SeedWalletAsync("USD", deposit: 0m, withdraw: 0m);
        var other = await SeedWalletAsync("USD", deposit: 0m, withdraw: 0m);

        await using var scope = fx.Scope();
        var service = scope.ServiceProvider.GetRequiredService<IPaymentBoxService>();
        await service.RenameBoxAsync(sub, new RenamePaymentBoxRequest("Sub", fx.PresetId, main));

        await Assert.ThrowsAsync<ArgumentException>(() =>   // its own main wallet
            service.RenameBoxAsync(main, new RenamePaymentBoxRequest("Main", fx.PresetId, main)));
        await Assert.ThrowsAsync<ArgumentException>(() =>   // the two-wallet loop
            service.RenameBoxAsync(main, new RenamePaymentBoxRequest("Main", fx.PresetId, sub)));
        await Assert.ThrowsAsync<ArgumentException>(() =>   // under a sub-wallet
            service.RenameBoxAsync(other, new RenamePaymentBoxRequest("Other", fx.PresetId, sub)));
        await Assert.ThrowsAsync<ArgumentException>(() =>   // a main wallet that has sub-wallets
            service.RenameBoxAsync(main, new RenamePaymentBoxRequest("Main", fx.PresetId, other)));
    }

    [SkippableFact]
    public async Task A_main_wallet_with_sub_wallets_cannot_be_deleted()
    {
        fx.RequireDatabase();
        var main = await SeedWalletAsync("USD", deposit: 100m, withdraw: 0m);
        var sub = await SeedWalletAsync("USD", deposit: 0m, withdraw: 0m);

        await using var scope = fx.Scope();
        var service = scope.ServiceProvider.GetRequiredService<IPaymentBoxService>();
        await service.RenameBoxAsync(sub, new RenamePaymentBoxRequest("Sub", fx.PresetId, main));

        await Assert.ThrowsAsync<ArgumentException>(() => service.DeleteBoxAsync(main));

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ChangeTracker.Clear();
        Assert.True(await db.PaymentTransactions.AnyAsync(t => t.PaymentBoxId == main));
    }

    /// <summary>Upgrades a separate database from the schema just before this feature, with rows already in it.</summary>
    [SkippableFact]
    public async Task Migration_leaves_existing_wallets_and_transactions_exactly_as_they_were()
    {
        fx.RequireDatabase();
        var cs = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable(StockDatabaseFixture.EnvVar))
        {
            InitialCatalog = $"ProcumentTest_{Guid.NewGuid():N}"
        }.ConnectionString;
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(cs, s => s.MigrationsAssembly("Procument.Data"))
            .Options;

        await using var db = new AppDbContext(options);
        var migrator = db.GetService<IMigrator>();
        try
        {
            await migrator.MigrateAsync("20261029052058_WalletTransactionMultiBase");

            var preset = new CompanyPreset { Name = "Existing company", IsActive = true };
            db.Add(preset);
            await db.SaveChangesAsync();

            // PaymentBoxes has no ParentBoxId yet at this schema, so these rows can't go through the model.
            foreach (var currency in new[] { "USD", "CNY", "AED" })
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO PaymentBoxes (Name, CompanyPresetId, Currency, CreatedAt, BankName) VALUES ({"Wallet " + currency}, {preset.Id}, {currency}, {DateTime.UtcNow}, {"Bank " + currency})");

            var boxIds = await db.Database.SqlQuery<long>($"SELECT Id AS [Value] FROM PaymentBoxes").ToListAsync();
            foreach (var boxId in boxIds)
                for (var i = 1; i <= 50; i++)
                    db.Add(new PaymentTransaction
                    {
                        PaymentBoxId = boxId,
                        Type = i % 3 == 0 ? "Withdraw" : "Deposit",
                        Amount = i * 11.37m,
                        ExchangeRate = i % 5 == 0 ? 7.2m : null,
                        FromType = "MotherWallet",
                        ToType = "MotherWallet",
                        Notes = $"row {i}",
                    });
            await db.SaveChangesAsync();

            var before = await FingerprintAsync(db);

            await migrator.MigrateAsync("20261101114746_AddWalletSubWallets");

            Assert.Equal(before, await FingerprintAsync(db));
            Assert.StartsWith("wallets=3 tx=150 ", before);
            Assert.Equal(0, await db.PaymentBoxes.CountAsync(b => b.ParentBoxId != null));
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    private static async Task<string> FingerprintAsync(AppDbContext db)
    {
        var rows = await db.Database.SqlQueryRaw<string>("""
            SELECT CONCAT(
                'wallets=', (SELECT COUNT(*) FROM PaymentBoxes),
                ' tx=', (SELECT COUNT(*) FROM PaymentTransactions),
                ' walletSum=', (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(Id, Name, CompanyPresetId, Currency, CreatedAt, BankName)) FROM PaymentBoxes),
                ' txSum=', (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM PaymentTransactions),
                ' balance=', (SELECT SUM(CASE WHEN [Type] = 'Deposit' THEN Amount * ISNULL(ExchangeRate, 1) ELSE -(Amount * ISNULL(ExchangeRate, 1)) END) FROM PaymentTransactions)
            ) AS [Value]
            """).ToListAsync();
        return rows.Single();
    }

    private async Task<long> SeedWalletAsync(string currency, decimal deposit, decimal withdraw)
    {
        await using var scope = fx.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var box = new PaymentBox { Name = $"Wallet-{Guid.NewGuid():N}", CompanyPresetId = fx.PresetId, Currency = currency };
        if (deposit > 0)
            box.Transactions.Add(new PaymentTransaction { Type = "Deposit", Amount = deposit, FromType = "MotherWallet", ToType = "MotherWallet" });
        if (withdraw > 0)
            box.Transactions.Add(new PaymentTransaction { Type = "Withdraw", Amount = withdraw, FromType = "MotherWallet", ToType = "MotherWallet" });
        db.PaymentBoxes.Add(box);
        await db.SaveChangesAsync();
        return box.Id;
    }
}
