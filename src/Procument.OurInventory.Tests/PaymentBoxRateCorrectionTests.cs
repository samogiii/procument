using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Procument.Data;
using Procument.Module.Purchasing.Entities;
using Procument.Module.Sales.Entities;
using Procument.Module.Sales.Services;
using Xunit;

namespace Procument.OurInventory.Tests;

[Collection(StockDatabaseCollection.Name)]
public sealed class PaymentBoxRateCorrectionTests(StockDatabaseFixture fx)
{
    [SkippableFact]
    public async Task Correcting_rate_rebalances_wallet_and_fee_without_changing_payment_amount_or_statuses()
    {
        fx.RequireDatabase();
        var seeded = await SeedPopPaymentAsync(rate: 7m, fee: 50m);

        await using var scope = fx.Scope();
        var service = scope.ServiceProvider.GetRequiredService<IPaymentBoxService>();
        var before = await service.GetByIdAsync(seeded.BoxId);
        Assert.Equal(300m, before!.Transactions.Single(t => t.Id == seeded.PaymentId).Balance);

        var result = await service.UpdateExchangeRateAsync(seeded.BoxId, seeded.PaymentId, 7.2m, "rate correction", fx.UserId);

        Assert.NotNull(result);
        Assert.Equal(250m, result.WalletBalance);
        Assert.Equal(30m, result.BankFeeAmount);
        Assert.Equal(280m, result.Transaction.Balance);
        Assert.False(result.Transaction.IsNew);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ChangeTracker.Clear();
        var payment = await db.PaymentTransactions.SingleAsync(t => t.Id == seeded.PaymentId);
        var fee = await db.PaymentTransactions.SingleAsync(t => t.PaymentRequestId == seeded.PaymentRequestId && t.ToType == "BankFee");
        var request = await db.Set<PaymentRequest>().SingleAsync(p => p.Id == seeded.PaymentRequestId);
        var po = await db.PurchaseOrders.SingleAsync(p => p.Id == seeded.PoId);
        Assert.Equal((100m, 7m, 7.2m), (payment.Amount, payment.OriginalExchangeRate, payment.ExchangeRate));
        Assert.Equal(30m, fee.Amount);
        Assert.NotNull(payment.RateEditedAt);
        Assert.Equal(fx.UserId, payment.RateEditedByUserId);
        Assert.Equal("PARTIALLY PAID", request.Status);
        Assert.Equal("Submitted", po.PaymentStatus);
        Assert.Equal(750m, payment.Amount * payment.ExchangeRate!.Value + fee.Amount);
    }

    [SkippableFact]
    public async Task Bank_fee_total_is_guarded_and_zero_fee_is_deleted()
    {
        fx.RequireDatabase();
        var rejected = await SeedPopPaymentAsync(rate: 7m, fee: 50m);
        await using (var scope = fx.Scope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IPaymentBoxService>();
            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                service.UpdateExchangeRateAsync(rejected.BoxId, rejected.PaymentId, 7.500001m, null, fx.UserId));
            Assert.Contains("make the bank fee negative", ex.Message);

            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ChangeTracker.Clear();
            Assert.Equal(7m, (await db.PaymentTransactions.SingleAsync(t => t.Id == rejected.PaymentId)).ExchangeRate);
            Assert.Equal(50m, (await db.PaymentTransactions.SingleAsync(t => t.PaymentRequestId == rejected.PaymentRequestId && t.ToType == "BankFee")).Amount);
        }

        var zero = await SeedPopPaymentAsync(rate: 7m, fee: 50m);
        await using (var scope = fx.Scope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IPaymentBoxService>();
            var result = await service.UpdateExchangeRateAsync(zero.BoxId, zero.PaymentId, 7.5m, null, fx.UserId);
            Assert.Equal(0m, result!.BankFeeAmount);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(await db.PaymentTransactions.AnyAsync(t => t.PaymentRequestId == zero.PaymentRequestId && t.ToType == "BankFee"));
        }
    }

    [SkippableFact]
    public async Task Review_toggle_controls_new_state_but_manual_rows_are_never_new()
    {
        fx.RequireDatabase();
        var seeded = await SeedPopPaymentAsync(rate: 7m, fee: 0m);
        await using var scope = fx.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var manual = new PaymentTransaction
        {
            PaymentBoxId = seeded.BoxId, Type = "Deposit", Amount = 1m,
            FromType = "MotherWallet", ToType = "MotherWallet", IsAuto = false,
        };
        db.Add(manual);
        await db.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<IPaymentBoxService>();
        Assert.True((await service.GetByIdAsync(seeded.BoxId))!.Transactions.Single(t => t.Id == seeded.PaymentId).IsNew);
        Assert.False((await service.GetByIdAsync(seeded.BoxId))!.Transactions.Single(t => t.Id == manual.Id).IsNew);

        Assert.False((await service.SetReviewedAsync(seeded.BoxId, seeded.PaymentId, true, fx.UserId))!.IsNew);
        Assert.True((await service.SetReviewedAsync(seeded.BoxId, seeded.PaymentId, false, fx.UserId))!.IsNew);
        Assert.False((await service.SetReviewedAsync(seeded.BoxId, manual.Id, false, fx.UserId))!.IsNew);

        var counts = await service.GetUnreviewedCountAsync();
        Assert.True(counts.ByBoxId.TryGetValue(seeded.BoxId, out var count));
        Assert.Equal(1, count);
    }

    private async Task<(long BoxId, long PaymentId, long PaymentRequestId, long PoId)> SeedPopPaymentAsync(decimal rate, decimal fee)
    {
        await using var scope = fx.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;
        var box = new PaymentBox { Name = $"Wallet-{Guid.NewGuid():N}", CompanyPresetId = fx.PresetId, Currency = "CNY" };
        var po = new PurchaseOrder
        {
            PONumber = $"PO-{Guid.NewGuid():N}"[..16], SupplierId = fx.SupplierId,
            Status = "Payment Done", PaymentStatus = "Submitted",
        };
        db.AddRange(box, po);
        await db.SaveChangesAsync();
        var request = new PaymentRequest { PRId = Random.Shared.Next(1000, 9999), POId = po.Id, Amount = 100m, Status = "PARTIALLY PAID" };
        db.Add(request);
        await db.SaveChangesAsync();

        db.Add(new PaymentTransaction
        {
            PaymentBoxId = box.Id, Type = "Deposit", Amount = 1000m,
            FromType = "MotherWallet", ToType = "MotherWallet", IsAuto = false, CreatedAt = now,
        });
        var payment = new PaymentTransaction
        {
            PaymentBoxId = box.Id, PaymentRequestId = request.Id, Type = "Withdraw", Amount = 100m,
            FromType = "MotherWallet", ToType = "Supplier", ToSupplierId = fx.SupplierId,
            TxCurrency = "USD", ExchangeRate = rate, IsAuto = true, PopUploadId = Guid.NewGuid(),
            CreatedAt = now.AddSeconds(1),
        };
        db.Add(payment);
        if (fee > 0)
        {
            db.Add(new PaymentTransaction
            {
                PaymentBoxId = box.Id, PaymentRequestId = request.Id, Type = "Withdraw", Amount = fee,
                FromType = "MotherWallet", ToType = "BankFee", TxCurrency = "CNY", IsAuto = true,
                CreatedAt = now.AddSeconds(2),
            });
        }
        await db.SaveChangesAsync();
        return (box.Id, payment.Id, request.Id, po.Id);
    }
}
