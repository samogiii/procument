using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Procument.Data;
using Procument.Module.Sales.DTOs;
using Procument.Module.Sales.Entities;
using Procument.Module.Sales.Services;
using Xunit;

namespace Procument.OurInventory.Tests;

/// <summary>Wallet surname (what the public warehouse server sees) and fixed bank fees.</summary>
[Collection(StockDatabaseCollection.Name)]
public sealed class WalletSurnameTests(StockDatabaseFixture fx)
{
    [Fact]
    public void A_wallet_with_a_surname_hides_its_name_and_company_from_the_warehouse()
    {
        Assert.Equal(("Blue", null), PaymentBox.ForWarehouse("Real Bank Account", "  Blue ", "Real Company Ltd"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_wallet_without_a_surname_still_sends_its_real_name(string? surname)
    {
        Assert.Equal(("Real Bank Account", "Real Company Ltd"), PaymentBox.ForWarehouse("Real Bank Account", surname, "Real Company Ltd"));
    }

    [SkippableFact]
    public async Task Surname_and_fees_are_saved_on_create_and_on_edit()
    {
        fx.RequireDatabase();
        await using var scope = fx.Scope();
        var boxes = scope.ServiceProvider.GetRequiredService<IPaymentBoxService>();

        var created = await boxes.CreateBoxAsync(new CreatePaymentBoxRequest(fx.PresetId, "CNY", $"Wallet-{Guid.NewGuid():N}",
            Surname: "  Blue ", HandlingFee: 30m, WireFee: 12.5m));
        Assert.Equal(("Blue", 30m, 12.5m), (created.Surname, created.HandlingFee!.Value, created.WireFee!.Value));

        var edited = await boxes.RenameBoxAsync(created.Id, new RenamePaymentBoxRequest(created.Name, fx.PresetId,
            Surname: "Green", HandlingFee: 45m, WireFee: null));

        Assert.Equal(("Green", 45m), (edited!.Surname, edited.HandlingFee!.Value));
        Assert.Null(edited.WireFee);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ChangeTracker.Clear();
        var saved = await db.PaymentBoxes.SingleAsync(b => b.Id == created.Id);
        Assert.Equal(("Green", 45m), (saved.Surname, saved.HandlingFee!.Value));
    }

    [SkippableFact]
    public async Task A_blank_surname_means_none_and_bad_fees_are_refused()
    {
        fx.RequireDatabase();
        await using var scope = fx.Scope();
        var boxes = scope.ServiceProvider.GetRequiredService<IPaymentBoxService>();
        var box = await boxes.CreateBoxAsync(new CreatePaymentBoxRequest(fx.PresetId, "USD", $"Wallet-{Guid.NewGuid():N}", Surname: "   "));
        Assert.Null(box.Surname);

        await Assert.ThrowsAsync<ArgumentException>(() => boxes.RenameBoxAsync(box.Id,
            new RenamePaymentBoxRequest(box.Name, fx.PresetId, HandlingFee: -1m)));
        await Assert.ThrowsAsync<ArgumentException>(() => boxes.RenameBoxAsync(box.Id,
            new RenamePaymentBoxRequest(box.Name, fx.PresetId, WireFee: 1.234m)));
    }
}
