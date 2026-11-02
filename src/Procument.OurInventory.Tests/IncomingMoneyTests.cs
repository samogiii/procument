using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Procument.Data;
using Procument.Module.Catalog.Entities;
using Procument.Module.RFQ.Entities;
using Procument.Module.Sales.Controllers;
using Procument.Module.Sales.Entities;
using Procument.Module.Sales.Services;
using Procument.Shared.Services;
using Xunit;

namespace Procument.OurInventory.Tests;

/// <summary>Customer POP → warehouse confirms → Wallets, Incoming Money: the wallet moves only on acceptance.</summary>
[Collection(StockDatabaseCollection.Name)]
public sealed class IncomingMoneyTests(StockDatabaseFixture fx)
{
    [SkippableFact]
    public async Task With_a_warehouse_a_pop_pays_the_pi_but_leaves_the_wallet_alone()
    {
        fx.RequireDatabase();
        var wallet = await SeedWalletAsync("CNY");
        var invoice = await SeedInvoiceAsync(wallet);

        await UploadPopAsync(invoice, 10_000m, "USD", 7.2m, warehouse: true);

        await using var scope = fx.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(10_000m, (await db.CustomerPayments.SingleAsync(p => p.InvoiceId == invoice)).Amount);
        var deposit = await db.IncomingDeposits.SingleAsync(d => d.CustomerPayment.InvoiceId == invoice);
        Assert.Equal((wallet, 10_000m, "USD", 7.2m, 72_000m, IncomingDeposit.Pending),
            (deposit.PaymentBoxId, deposit.Amount, deposit.Currency, deposit.WalletRate!.Value, deposit.OfficeAmount, deposit.Status));
        Assert.False(await db.PaymentTransactions.AnyAsync(t => t.PaymentBoxId == wallet));
    }

    [SkippableFact]
    public async Task Without_a_warehouse_a_pop_deposits_at_once_as_before()
    {
        fx.RequireDatabase();
        var wallet = await SeedWalletAsync("USD");
        var invoice = await SeedInvoiceAsync(wallet);

        await UploadPopAsync(invoice, 500m, "USD", null, warehouse: false);

        await using var scope = fx.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.IncomingDeposits.AnyAsync(d => d.CustomerPayment.InvoiceId == invoice));
        Assert.Equal(("Deposit", 500m), await db.PaymentTransactions.Where(t => t.PaymentBoxId == wallet)
            .Select(t => ValueTuple.Create(t.Type, t.Amount)).SingleAsync());
    }

    [SkippableFact]
    public async Task Accepting_books_the_deposit_less_the_bank_fee()
    {
        fx.RequireDatabase();
        var wallet = await SeedWalletAsync("CNY");
        var id = await SeedDepositAsync(wallet, 10_000m, "USD", 7.2m);
        await ConfirmAsync(id, 9_950m, 7.2m);

        await using var scope = fx.Scope();
        var result = Assert.IsType<OkObjectResult>(await Controller(scope).Accept(id));
        Assert.Equal(360m, Read(result).GetProperty("bankFee").GetDecimal());

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ChangeTracker.Clear();
        var rows = await db.PaymentTransactions.Where(t => t.PaymentBoxId == wallet).ToListAsync();
        var deposit = rows.Single(t => t.Type == "Deposit");
        var fee = rows.Single(t => t.Type == "Withdraw");

        // Procument − bank fee = what the warehouse says reached the bank.
        Assert.Equal(("USD", 10_000m, 7.2m), (deposit.TxCurrency, deposit.Amount, deposit.ExchangeRate!.Value));
        Assert.Equal(("BankFee", "CNY", 360m), (fee.ToType, fee.TxCurrency, fee.Amount));
        Assert.Equal(71_640m, deposit.Amount * deposit.ExchangeRate!.Value - fee.Amount);
        Assert.NotNull(deposit.InvoiceId);
        Assert.Equal(deposit.InvoiceId, fee.InvoiceId);

        var row = await db.IncomingDeposits.SingleAsync(d => d.Id == id);
        Assert.Equal((IncomingDeposit.Accepted, 360m, deposit.Id, fee.Id, deposit.PopUploadId!.Value),
            (row.Status, row.BankFee!.Value, row.DepositTransactionId!.Value, row.FeeTransactionId!.Value, row.UploadId));
    }

    [SkippableFact]
    public async Task Accepted_money_reaches_the_wallet_with_auto_import_off()
    {
        fx.RequireDatabase();
        var wallet = await SeedWalletAsync("USD");
        var id = await SeedDepositAsync(wallet, 1_000m, "USD", null);
        await ConfirmAsync(id, 975m, null);

        await using var scope = fx.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await WalletImport.IsOnAsync(db));
        Assert.IsType<OkObjectResult>(await Controller(scope).Accept(id));

        db.ChangeTracker.Clear();
        var rows = await db.PaymentTransactions.Where(t => t.PaymentBoxId == wallet).ToListAsync();
        Assert.All(rows, t => { Assert.True(t.InWallet); Assert.NotNull(t.ReviewedAt); });
        Assert.Equal(1_000m, rows.Single(t => t.Type == "Deposit").Amount);
        Assert.Equal(25m, rows.Single(t => t.ToType == "BankFee").Amount);   // same currency: no rate on either side
    }

    [SkippableFact]
    public async Task More_than_expected_deposits_what_arrived_and_books_no_fee()
    {
        fx.RequireDatabase();
        var wallet = await SeedWalletAsync("CNY");
        var id = await SeedDepositAsync(wallet, 10_000m, "USD", 7.2m);
        await ConfirmAsync(id, 10_000m, 7.4m);   // 74,000 against Procument's 72,000

        await using var scope = fx.Scope();
        Assert.IsType<OkObjectResult>(await Controller(scope).Accept(id));

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ChangeTracker.Clear();
        var only = await db.PaymentTransactions.SingleAsync(t => t.PaymentBoxId == wallet);
        Assert.Equal(("Deposit", 10_000m, 7.4m), (only.Type, only.Amount, only.ExchangeRate!.Value));
        Assert.Contains("2000.00 CNY more", only.Notes);
        Assert.Equal(0m, (await db.IncomingDeposits.SingleAsync(d => d.Id == id)).BankFee);
    }

    [SkippableFact]
    public async Task Money_needs_the_warehouse_first_and_is_accepted_only_once()
    {
        fx.RequireDatabase();
        var wallet = await SeedWalletAsync("USD");
        var id = await SeedDepositAsync(wallet, 200m, "USD", null);

        await using var scope = fx.Scope();
        var controller = Controller(scope);
        Assert.IsType<BadRequestObjectResult>(await controller.Accept(id));
        await ConfirmAsync(id, 200m, null);
        Assert.IsType<OkObjectResult>(await controller.Accept(id));
        Assert.IsType<BadRequestObjectResult>(await controller.Accept(id));
        Assert.IsType<BadRequestObjectResult>(await controller.SendBack(id, new SendBackDepositRequest("Too late")));

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ChangeTracker.Clear();
        Assert.Equal(1, await db.PaymentTransactions.CountAsync(t => t.PaymentBoxId == wallet));
    }

    [SkippableFact]
    public async Task Sending_back_needs_a_note_clears_the_result_and_moves_no_money()
    {
        fx.RequireDatabase();
        var wallet = await SeedWalletAsync("USD");
        var id = await SeedDepositAsync(wallet, 300m, "USD", null);
        await ConfirmAsync(id, 280m, null);

        await using var scope = fx.Scope();
        var controller = Controller(scope);
        Assert.IsType<BadRequestObjectResult>(await controller.SendBack(id, new SendBackDepositRequest("  ")));
        Assert.IsType<OkObjectResult>(await controller.SendBack(id, new SendBackDepositRequest("Wrong price")));
        Assert.IsType<BadRequestObjectResult>(await controller.Accept(id));

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ChangeTracker.Clear();
        var row = await db.IncomingDeposits.SingleAsync(d => d.Id == id);
        Assert.Equal((IncomingDeposit.Pending, "Wrong price"), (row.Status, row.RejectNote));
        Assert.Null(row.BankWalletAmount);
        Assert.Null(row.BankAt);
        Assert.False(await db.PaymentTransactions.AnyAsync(t => t.PaymentBoxId == wallet));
    }

    [SkippableFact]
    public async Task Deleting_an_accepted_pop_takes_its_wallet_rows_with_it()
    {
        fx.RequireDatabase();
        var wallet = await SeedWalletAsync("USD");
        var invoice = await SeedInvoiceAsync(wallet);
        var fileName = await UploadPopAsync(invoice, 1_000m, "USD", null, warehouse: true);

        long id;
        await using (var scope = fx.Scope())
            id = await scope.ServiceProvider.GetRequiredService<AppDbContext>().IncomingDeposits
                .Where(d => d.CustomerPayment.InvoiceId == invoice).Select(d => d.Id).SingleAsync();
        await ConfirmAsync(id, 990m, null);

        await using (var scope = fx.Scope())
        {
            Assert.IsType<OkObjectResult>(await Controller(scope).Accept(id));
            Assert.IsType<OkObjectResult>(await Documents(scope, warehouse: true).DeletePI(invoice, fileName, "customer_pop"));
        }

        await using var check = fx.Scope();
        var db = check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.IncomingDeposits.AnyAsync(d => d.Id == id));
        Assert.False(await db.CustomerPayments.AnyAsync(p => p.InvoiceId == invoice));
        Assert.False(await db.PaymentTransactions.AnyAsync(t => t.PaymentBoxId == wallet));
    }

    [SkippableFact]
    public async Task A_wallet_with_incoming_money_cannot_be_deleted()
    {
        fx.RequireDatabase();
        var wallet = await SeedWalletAsync("USD");
        await SeedDepositAsync(wallet, 50m, "USD", null);

        await using var scope = fx.Scope();
        var boxes = scope.ServiceProvider.GetRequiredService<IPaymentBoxService>();
        await Assert.ThrowsAsync<ArgumentException>(() => boxes.DeleteBoxAsync(wallet));
    }

    [SkippableFact]
    public async Task List_shows_ready_money_first_with_the_fee_it_would_book()
    {
        fx.RequireDatabase();
        var wallet = await SeedWalletAsync("CNY");
        var id = await SeedDepositAsync(wallet, 100m, "USD", 7m);
        await ConfirmAsync(id, 99m, 7m);

        await using var scope = fx.Scope();
        var result = Assert.IsType<OkObjectResult>(await Controller(scope).List());
        var rows = Read(result).EnumerateArray().ToList();

        Assert.Equal("Waiting for acceptance in Wallets", rows[0].GetProperty("stage").GetString());
        var row = rows.Single(r => r.GetProperty("id").GetInt64() == id);
        Assert.Equal((700m, 693m, 7m), (row.GetProperty("officeAmount").GetDecimal(), row.GetProperty("bankWalletAmount").GetDecimal(), row.GetProperty("difference").GetDecimal()));
        Assert.StartsWith("C", row.GetProperty("customer").GetString());
    }

    // ── Helpers ──

    /// <summary>As the browser reads it.</summary>
    private static JsonElement Read(OkObjectResult result)
        => JsonSerializer.SerializeToElement(result.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static IncomingMoneyController Controller(AsyncServiceScope scope) => new(
        scope.ServiceProvider.GetRequiredService<DbContext>(),
        scope.ServiceProvider.GetRequiredService<IDocumentStorageService>())
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
    };

    private static DocumentsController Documents(AsyncServiceScope scope, bool warehouse)
    {
        var settings = new Dictionary<string, string?> { ["WarehouseSync:Url"] = warehouse ? "https://warehouse.test" : "" };
        return new DocumentsController(
            scope.ServiceProvider.GetRequiredService<DbContext>(),
            scope.ServiceProvider.GetRequiredService<IDocumentStorageService>(),
            scope.ServiceProvider.GetRequiredService<IPaymentBoxService>(),
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
    }

    private async Task<string> UploadPopAsync(long invoiceId, decimal amount, string currency, decimal? rate, bool warehouse)
    {
        await using var scope = fx.Scope();
        await using var content = new MemoryStream([1, 2, 3]);
        var file = new FormFile(content, 0, content.Length, "file", $"pop-{Guid.NewGuid():N}.pdf");
        var result = Assert.IsType<OkObjectResult>(
            await Documents(scope, warehouse).UploadCustomerPop(invoiceId, file, amount, currency: currency, exchangeRate: rate));
        return Read(result).GetProperty("fileName").GetString()!;
    }

    private async Task<long> SeedWalletAsync(string currency)
    {
        await using var scope = fx.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var box = new PaymentBox { Name = $"Wallet-{Guid.NewGuid():N}", CompanyPresetId = fx.PresetId, Currency = currency };
        db.PaymentBoxes.Add(box);
        await db.SaveChangesAsync();
        return box.Id;
    }

    private async Task<long> SeedInvoiceAsync(long walletId)
    {
        await using var scope = fx.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var customer = new Customer { Name = $"Cust-{Guid.NewGuid():N}"[..12], CustomerCode = $"C{Guid.NewGuid():N}"[..8] };
        var rfq = new RFQHeader { Name = $"RFQ-{Guid.NewGuid():N}"[..12], Customer = customer, UserId = fx.UserId, LeadTime = DateTime.UtcNow.AddDays(5), ReceivedDate = DateTime.UtcNow };
        var quote = new Quote { QuoteNumber = $"Q-{Guid.NewGuid():N}"[..10], RFQ = rfq, Customer = customer, UserId = fx.UserId, Status = "Accepted" };
        var invoice = new Invoice
        {
            InvoiceNumber = $"SO-{Guid.NewGuid():N}"[..10], Quote = quote, Customer = customer, Status = "Running",
            TotalAmount = 100_000m, DefaultDepositWalletId = walletId,
        };
        db.Add(invoice);
        await db.SaveChangesAsync();
        return invoice.Id;
    }

    /// <summary>A POP uploaded in Procument, not confirmed yet.</summary>
    private async Task<long> SeedDepositAsync(long walletId, decimal amount, string currency, decimal? rate)
    {
        var invoiceId = await SeedInvoiceAsync(walletId);
        await using var scope = fx.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var payment = new CustomerPayment { InvoiceId = invoiceId, FileName = $"pop-{Guid.NewGuid():N}.pdf", Amount = amount, ReceivedAmount = amount, Currency = currency };
        db.CustomerPayments.Add(payment);
        await db.SaveChangesAsync();
        var deposit = new IncomingDeposit
        {
            CustomerPaymentId = payment.Id, PaymentBoxId = walletId, Amount = amount, Currency = currency,
            WalletRate = rate, OfficeAmount = IncomingDeposit.WalletAmount(amount, rate),
        };
        db.IncomingDeposits.Add(deposit);
        await db.SaveChangesAsync();
        return deposit.Id;
    }

    /// <summary>What the sync stores when the phone confirmed it.</summary>
    private async Task ConfirmAsync(long id, decimal amount, decimal? rate)
    {
        await using var scope = fx.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.IncomingDeposits.SingleAsync(d => d.Id == id);
        row.BankPopFileName = "bank.jpg";
        row.BankAmount = amount;
        row.BankRate = rate;
        row.BankWalletAmount = IncomingDeposit.WalletAmount(amount, rate);
        row.BankBy = "Phone";
        row.BankAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }
}
