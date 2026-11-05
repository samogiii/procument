using Procument.Module.Sales.Entities;
using Procument.Module.Sales.Services;
using Xunit;

namespace Procument.OurInventory.Tests;

/// <summary>Sam's fee rules on top of what the POP reader saw (PopFees). No database.</summary>
public sealed class PopFeesTests
{
    private static PopReading Read(decimal? amount, string? currency, decimal? fees = null, decimal? total = null,
        string? totalCurrency = null, decimal? confidence = 0.95m) => new()
    {
        Status = PopReading.Done, Amount = amount, Currency = currency, Fees = fees, FeesCurrency = fees != null ? currency : null,
        TotalDebited = total, TotalDebitedCurrency = totalCurrency ?? (total != null ? currency : null), Confidence = confidence,
    };

    [Fact]
    public void Yuan_transfer_has_no_wire_fee_only_the_wallets_handling_fee()
    {
        var p = PopFees.Transfer("CNY", "CNY", 500m, null, 200m, 45m, WireFeePayer.Ours, Read(500m, "CNY"));
        Assert.Equal(500m, p.Amount);
        Assert.Equal(200m, p.HandlingFee);
        Assert.Equal(0m, p.SourceWireFee);
        Assert.Equal(0m, p.DestWireFee);
        Assert.Equal(700m, p.Debit);
        Assert.Equal(500m, p.Credit);
    }

    [Theory]
    [InlineData(WireFeePayer.Ours, 30, 0)]
    [InlineData(WireFeePayer.Split, 15, 106.65)]
    [InlineData(WireFeePayer.Theirs, 0, 213.30)]
    public void Wire_fee_is_split_by_who_pays_it(string payer, double ours, double theirs)
    {
        var p = PopFees.Transfer("USD", "CNY", 1000m, 7.11m, 10m, 30m, payer, null);
        Assert.Equal((decimal)ours, p.SourceWireFee);
        Assert.Equal((decimal)theirs, p.DestWireFee);
        Assert.Equal(7110m, p.Received);
    }

    [Fact]
    public void Fee_shown_on_the_pop_is_ours_and_replaces_our_part_of_the_wire_fee()
    {
        var p = PopFees.Transfer("USD", "CNY", 1000m, 7.11m, 10m, 30m, WireFeePayer.Split, Read(1000m, "USD", fees: 25m));
        Assert.Equal(25m, p.BankFee);
        Assert.Equal(0m, p.SourceWireFee);
        Assert.Equal(106.65m, p.DestWireFee);    // their half still comes off what arrives
        Assert.Equal(1035m, p.Debit);
    }

    [Fact]
    public void Pop_amount_above_the_office_amount_is_a_bank_fee()
    {
        var p = PopFees.Withdraw("USD", "USD", 1000m, null, 5m, 20m, WireFeePayer.Ours, Read(1025m, "USD"));
        Assert.Equal(1000m, p.Amount);
        Assert.Equal(25m, p.BankFee);
        Assert.Equal(0m, p.WireFee);
        Assert.Equal(1030m, p.Total);
    }

    [Fact]
    public void Total_debited_in_the_wallet_currency_gives_the_fee_over_amount_times_rate()
    {
        var p = PopFees.Withdraw("CNY", "USD", 1000m, 7.1m, 200m, 45m, WireFeePayer.Ours, Read(1000m, "USD", total: 7130m, totalCurrency: "CNY"));
        Assert.Equal(7100m, p.WalletAmount);
        Assert.Equal(30m, p.BankFee);
        Assert.Equal(0m, p.WireFee);
        Assert.Equal(7330m, p.Total);
    }

    [Fact]
    public void Withdraw_without_a_pop_fee_uses_our_part_of_the_wallet_wire_fee()
    {
        Assert.Equal(45m, PopFees.Withdraw("CNY", "USD", 1000m, 7.1m, 200m, 45m, WireFeePayer.Ours, Read(1000m, "USD")).WireFee);
        Assert.Equal(22.5m, PopFees.Withdraw("CNY", "USD", 1000m, 7.1m, 200m, 45m, WireFeePayer.Split, Read(1000m, "USD")).WireFee);
        Assert.Equal(0m, PopFees.Withdraw("CNY", "USD", 1000m, 7.1m, 200m, 45m, WireFeePayer.Theirs, Read(1000m, "USD")).WireFee);
    }

    [Fact]
    public void Deposit_ignores_the_senders_wire_fee()
    {
        var p = PopFees.Deposit("CNY", "USD", 1000m, 7.11m, Read(990m, "USD", fees: 20m));
        Assert.Equal(990m, p.Amount);
        Assert.Equal(7038.90m, p.WalletAmount);
        Assert.Contains(p.Notes, n => n.Contains("customer"));
    }

    [Fact]
    public void Unread_or_wrong_currency_pop_falls_back_to_the_office_amount()
    {
        Assert.Equal(1000m, PopFees.Transfer("USD", "CNY", 1000m, 7.11m, 0m, 0m, WireFeePayer.Ours, null).Amount);
        var p = PopFees.Withdraw("USD", "USD", 1000m, null, 0m, 0m, WireFeePayer.Ours, Read(7100m, "CNY"));
        Assert.Equal(1000m, p.Amount);
        Assert.Equal(0m, p.BankFee);
        Assert.NotEmpty(p.Notes);
    }

    [Fact]
    public void Yuan_pop_for_a_dollar_payment_is_converted_with_the_rate_and_checked()
    {
        // Supplier payment $1,000 from a ¥ wallet at BOC 7.10 + 0.2 = 7.30; the bank's POP only shows ¥7,100.
        var p = PopFees.Withdraw("CNY", "USD", 1000m, 7.30m, 0m, 0m, WireFeePayer.Ours, Read(7100m, "CNY"));
        Assert.NotNull(p.Check);
        Assert.Equal(7100m, p.Check!.PopAmount);
        Assert.Equal(7.30m, p.Check.Rate);
        Assert.Equal(972.60m, p.Check.InSetCurrency);   // 7100 ÷ 7.30
        Assert.Equal(-27.40m, p.Check.Difference);
        Assert.Equal(7.1m, p.Check.PopRate);
        Assert.False(p.Check.Matches);

        var exact = PopFees.Withdraw("CNY", "USD", 1000m, 7.30m, 0m, 0m, WireFeePayer.Ours, Read(7300m, "CNY")).Check!;
        Assert.True(exact.Matches);
        Assert.Equal(1000m, exact.InSetCurrency);
    }

    [Fact]
    public void Pop_in_the_set_currency_must_match_to_the_cent()
    {
        Assert.True(PopFees.Withdraw("CNY", "USD", 1000m, 7.3m, 0m, 0m, WireFeePayer.Ours, Read(1000m, "USD")).Check!.Matches);
        var more = PopFees.Withdraw("CNY", "USD", 1000m, 7.3m, 0m, 0m, WireFeePayer.Ours, Read(1025m, "USD")).Check!;
        Assert.False(more.Matches);
        Assert.Equal(25m, more.Difference);
        Assert.Null(PopFees.Withdraw("CNY", "USD", 1000m, 7.3m, 0m, 0m, WireFeePayer.Ours, null).Check);
    }

    [Fact]
    public void Transfer_pop_in_the_destination_currency_goes_back_through_the_rate()
    {
        // ¥ → $ transfer of ¥7,300 at "1 USD = 7.30 CNY"; the POP shows $1,000 credited.
        var c = PopFees.Transfer("CNY", "USD", 7300m, 7.30m, 0m, 0m, WireFeePayer.Ours, Read(1000m, "USD")).Check!;
        Assert.Equal(7300m, c.InSetCurrency);
        Assert.True(c.Matches);
        Assert.Equal("1 USD = 7.3 CNY", c.RateText);
    }

    [Fact]
    public void Low_confidence_is_flagged()
        => Assert.Contains(PopFees.Withdraw("USD", "USD", 1000m, null, 0m, 0m, WireFeePayer.Ours, Read(1000m, "USD", confidence: 0.3m)).Notes,
            n => n.Contains("not sure"));
}
