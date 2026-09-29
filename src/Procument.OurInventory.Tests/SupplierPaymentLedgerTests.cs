using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Procument.Data;
using Procument.Module.Catalog.Entities;
using Procument.Module.OurInventory.Services;
using Procument.Module.Purchasing.Entities;
using Procument.Module.Purchasing.Services;
using Procument.Module.Sales.Entities;
using Procument.Module.Sales.Services;
using Procument.Shared.Services;
using Xunit;

namespace Procument.OurInventory.Tests;

/// <summary>
/// Supplier Credits E2: every "paid" figure is wallet payments + net credit applied, and nothing else.
/// </summary>
[Collection(StockDatabaseCollection.Name)]
public sealed class SupplierPaymentLedgerTests(StockDatabaseFixture fx)
{
    [SkippableFact]
    public async Task Paid_is_wallet_payments_plus_net_credit_and_ignores_bank_fees_and_top_ups()
    {
        fx.RequireDatabase();
        var s = await SeedAsync(poTotal: 100m);
        await using (var scope = fx.Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            AddWallet(db, s, "Supplier", 40m, s.PrId);
            AddWallet(db, s, "BankFee", 5m, s.PrId);
            // Deliberately tagged with the PR: the ToType filter alone must keep a top-up out of "paid".
            AddWallet(db, s, "SupplierCredit", 500m, s.PrId);
            await db.SaveChangesAsync();

            var first = AddCredit(db, s, SupplierCreditDirections.Out, SupplierCreditSources.POApplication, 30m);
            var second = AddCredit(db, s, SupplierCreditDirections.Out, SupplierCreditSources.POApplication, 10m);
            await db.SaveChangesAsync();
            AddCredit(db, s, SupplierCreditDirections.In, SupplierCreditSources.Reversal, 10m, reverses: second.Id);
            await db.SaveChangesAsync();
            Assert.NotEqual(0, first.Id);
        }

        await using (var scope = fx.Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(70m, await SupplierPaymentLedger.PaidByPrAsync(db, s.PrId));
            Assert.Equal(70m, await SupplierPaymentLedger.PaidByPoAsync(db, s.PoId));
            Assert.Equal(30m, (await SupplierPaymentLedger.CreditAppliedByPrAsync(db, [s.PrId]))[s.PrId]);

            var ledger = scope.ServiceProvider.GetRequiredService<IPaymentLedgerService>();
            Assert.Equal(70m, (await ledger.GetSupplierPaidAmountsAsync([s.PrId]))[s.PrId]);

            var pr = await scope.ServiceProvider.GetRequiredService<IPaymentRequestService>().GetByIdAsync(s.PrId);
            Assert.Equal((70m, 30m, 30m), (pr.PaidAmount, pr.CreditApplied, pr.RemainingAmount));
        }
    }

    [SkippableFact]
    public async Task Wallet_only_payments_are_unchanged_when_no_credit_exists()
    {
        fx.RequireDatabase();
        var s = await SeedAsync(poTotal: 100m);
        await using var scope = fx.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        AddWallet(db, s, "Supplier", 60m, s.PrId);
        await db.SaveChangesAsync();

        Assert.Equal(60m, await SupplierPaymentLedger.PaidByPrAsync(db, s.PrId));
        Assert.Empty(await SupplierPaymentLedger.CreditAppliedByPrAsync(db, [s.PrId]));
        var pr = await scope.ServiceProvider.GetRequiredService<IPaymentRequestService>().GetByIdAsync(s.PrId);
        Assert.Equal((60m, 0m, 40m), (pr.PaidAmount, pr.CreditApplied, pr.RemainingAmount));
    }

    [SkippableFact]
    public async Task Completion_marks_pr_paid_and_po_payment_done_when_credit_covers_the_rest()
    {
        fx.RequireDatabase();
        var s = await SeedAsync(poTotal: 100m);
        await using var scope = fx.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        AddWallet(db, s, "Supplier", 25m, s.PrId);
        AddCredit(db, s, SupplierCreditDirections.Out, SupplierCreditSources.POApplication, 75m);
        await db.SaveChangesAsync();

        var po = await db.PurchaseOrders.Include(p => p.ImportDetail).SingleAsync(p => p.Id == s.PoId);
        var request = await db.Set<PaymentRequest>().SingleAsync(p => p.Id == s.PrId);
        var progress = await SupplierPaymentCompletion.ApplyAsync(db, po, request, fx.UserId);
        await db.SaveChangesAsync();

        Assert.Equal(new SupplierPaymentProgress(100m, 100m, 100m, true), progress);
        Assert.Equal("PAID", request.Status);
        Assert.Equal("Submitted", po.PaymentStatus);
        Assert.Equal(PurchaseOrderStatusFlow.PaymentDone, po.Status);
        Assert.Equal(fx.UserId, po.PaymentSubmittedBy);
    }

    [SkippableFact]
    public async Task Completion_leaves_po_partially_paid_while_a_balance_remains()
    {
        fx.RequireDatabase();
        var s = await SeedAsync(poTotal: 100m);
        await using var scope = fx.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        AddCredit(db, s, SupplierCreditDirections.Out, SupplierCreditSources.POApplication, 40m);
        await db.SaveChangesAsync();

        var po = await db.PurchaseOrders.Include(p => p.ImportDetail).SingleAsync(p => p.Id == s.PoId);
        var request = await db.Set<PaymentRequest>().SingleAsync(p => p.Id == s.PrId);
        var progress = await SupplierPaymentCompletion.ApplyAsync(db, po, request, fx.UserId);

        Assert.False(progress.Complete);
        Assert.Equal(60m, progress.PoRemaining);
        Assert.Equal("PARTIALLY PAID", request.Status);
        Assert.Equal("PartiallyPaid", po.PaymentStatus);
        Assert.Null(po.PaymentSubmittedAt);
    }

    [SkippableFact]
    public async Task Stock_po_paid_amount_raw_sql_includes_credit()
    {
        fx.RequireDatabase();
        var part = await fx.NewPartAsync();
        var stockPo = await fx.CreateApprovedStockPoAsync(fx.MainWarehouseId, (part.Id, 2, 50m, "NE"));

        await using var scope = fx.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var request = new PaymentRequest { PRId = Random.Shared.Next(100000, 999999), POId = stockPo.Id, Amount = 100m, Status = "PARTIALLY PAID" };
        db.Add(request);
        await db.SaveChangesAsync();
        var s = new Seed(fx.SupplierId, fx.PresetId, stockPo.Id, request.Id, await BoxAsync(db), await AccountAsync(db, fx.SupplierId, fx.PresetId));
        AddWallet(db, s, "Supplier", 30m, request.Id);
        AddCredit(db, s, SupplierCreditDirections.Out, SupplierCreditSources.POApplication, 45m);
        await db.SaveChangesAsync();

        var reloaded = await scope.ServiceProvider.GetRequiredService<IStockPurchaseOrderService>().GetByIdAsync(stockPo.Id);
        Assert.Equal(75m, reloaded!.PaidAmount);
    }

    // ── Seeding ────────────────────────────────────────────────────────────

    private sealed record Seed(long SupplierId, long PresetId, long PoId, long PrId, long BoxId, long AccountId);

    /// <summary>A fresh supplier per test, so credit accounts (unique per supplier × company) never collide.</summary>
    private async Task<Seed> SeedAsync(decimal poTotal)
    {
        await using var scope = fx.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var supplier = new Supplier { Name = $"Credit Supplier {Guid.NewGuid():N}"[..30], Status = "Approved", IsActive = true };
        db.Add(supplier);
        await db.SaveChangesAsync();

        var po = new PurchaseOrder
        {
            PONumber = $"PO-{Guid.NewGuid():N}"[..16], SupplierId = supplier.Id, CompanyPresetId = fx.PresetId,
            TotalAmount = poTotal, Status = PurchaseOrderStatusFlow.WaitingForPayment,
            AdminApproval = "Approved", PaymentApproval = "Accepted",
        };
        db.Add(po);
        await db.SaveChangesAsync();
        var request = new PaymentRequest { PRId = Random.Shared.Next(100000, 999999), POId = po.Id, Amount = poTotal, Status = "PENDING APPROVAL" };
        db.Add(request);
        await db.SaveChangesAsync();

        return new Seed(supplier.Id, fx.PresetId, po.Id, request.Id, await BoxAsync(db), await AccountAsync(db, supplier.Id, fx.PresetId));
    }

    private async Task<long> BoxAsync(AppDbContext db)
    {
        var box = new PaymentBox { Name = $"Wallet-{Guid.NewGuid():N}", CompanyPresetId = fx.PresetId, Currency = "USD" };
        db.Add(box);
        await db.SaveChangesAsync();
        return box.Id;
    }

    private static async Task<long> AccountAsync(AppDbContext db, long supplierId, long presetId)
    {
        var account = await db.SupplierCreditAccounts.FirstOrDefaultAsync(a =>
            a.SupplierId == supplierId && a.CompanyPresetId == presetId && a.Currency == "USD");
        if (account != null) return account.Id;
        account = new SupplierCreditAccount { SupplierId = supplierId, CompanyPresetId = presetId, Balance = 1000m };
        db.Add(account);
        await db.SaveChangesAsync();
        return account.Id;
    }

    private static void AddWallet(AppDbContext db, Seed s, string toType, decimal amount, long? prId) =>
        db.Add(new PaymentTransaction
        {
            PaymentBoxId = s.BoxId, PaymentRequestId = prId, Type = "Withdraw", Amount = amount,
            FromType = "MotherWallet", ToType = toType, ToSupplierId = toType == "BankFee" ? null : s.SupplierId,
            TxCurrency = "USD", IsAuto = true,
        });

    /// <summary>Ledger rows written directly; BalanceAfter is not what these tests check.</summary>
    private static SupplierCreditEntry AddCredit(AppDbContext db, Seed s, string direction, string source, decimal amount, long? reverses = null)
    {
        var entry = new SupplierCreditEntry
        {
            AccountId = s.AccountId, Direction = direction, Source = source, Amount = amount, BalanceAfter = 0m,
            PurchaseOrderId = s.PoId, PaymentRequestId = s.PrId, ReversesEntryId = reverses, RequestId = Guid.NewGuid(),
        };
        db.Add(entry);
        return entry;
    }
}
