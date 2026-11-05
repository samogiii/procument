using Procument.Module.Sales.Entities;

namespace Procument.Module.Sales.Services;

/// <summary>What the POP reader saw, as the Wallets page shows it.</summary>
public record PopReadingDto(
    string Status, string? Error, decimal? Amount, string? Currency, decimal? Fees, string? FeesCurrency,
    decimal? TotalDebited, string? TotalDebitedCurrency, decimal? ReceivedAmount, string? ReceivedCurrency,
    decimal? ExchangeRate, string? ChargesOption, string? ValueDate, string? Reference, decimal? Confidence,
    string? Note, bool? IsPaymentProof, DateTime? ReadAt)
{
    public static PopReadingDto? From(PopReading? r) => r == null ? null : new(
        r.Status, r.Error, r.Amount, r.Currency, r.Fees, r.FeesCurrency, r.TotalDebited, r.TotalDebitedCurrency,
        r.ReceivedAmount, r.ReceivedCurrency, r.ExchangeRate, r.ChargesOption, r.ValueDate, r.Reference, r.Confidence,
        r.Note, r.IsPaymentProof, r.ReadAt);
}

/// <summary>
/// The price on the POP against the price the office set. When the POP is in the other currency (e.g. a yuan POP for a
/// dollar payment) it is converted with the saved rate: <c>PopAmount ÷ or × Rate = InSetCurrency</c>.
/// <see cref="PopRate"/> is the rate the POP itself works out at (other currency per 1 set currency), when it can.
/// </summary>
public record PopCheck(
    decimal PopAmount, string PopCurrency, decimal? Rate, string? RateText, decimal InSetCurrency, decimal SetAmount,
    string SetCurrency, decimal Difference, decimal? PopRate, bool Matches);

/// <summary>Proposed transfer numbers. Source loses Debit (From currency), destination gets Credit (To currency).</summary>
public record TransferProposal(
    decimal Amount, decimal? Rate, decimal Received, decimal HandlingFee, decimal BankFee, decimal SourceWireFee,
    decimal DestWireFee, string WireFeePaidBy, List<string> Notes)
{
    public decimal Debit => Amount + HandlingFee + BankFee + SourceWireFee;
    public decimal Credit => Received - DestWireFee;
    public PopCheck? Check { get; init; }
}

/// <summary>Proposed withdraw numbers: Amount in the withdraw currency, everything else in the wallet currency.</summary>
public record WithdrawProposal(
    decimal Amount, decimal? Rate, decimal WalletAmount, decimal HandlingFee, decimal BankFee, decimal WireFee,
    string WireFeePaidBy, List<string> Notes)
{
    public decimal Total => WalletAmount + HandlingFee + BankFee + WireFee;
    public PopCheck? Check { get; init; }
}

/// <summary>Proposed deposit numbers: what the customer sent (POP currency) and the rate into the wallet.</summary>
public record DepositProposal(decimal Amount, decimal? Rate, decimal WalletAmount, List<string> Notes)
{
    public PopCheck? Check { get; init; }
}

/// <summary>
/// Sam's fee rules, worked out from the POP reader's numbers (all of them only a proposal: the office can change any
/// before it accepts).
/// <list type="bullet">
/// <item>Handling fee: always the sending wallet's own (never on a POP).</item>
/// <item>Bank fee: when the POP shows more leaving our account than the amount the office set (fees on it, a bigger
/// total, or a bigger amount), the difference is a fee we paid.</item>
/// <item>Wire fee: the sending wallet's fixed wire fee, split by WireFeePaidBy (Ours / Split / Theirs). When the POP
/// already shows a fee we paid, that replaces our part of it. Transfers from a yuan wallet have no wire fee.</item>
/// <item>Deposits: fees on the POP are the customer's, never ours; only the amount sent counts.</item>
/// </list>
/// </summary>
public static class PopFees
{
    /// <summary>Below this the reading is flagged for a closer look.</summary>
    public const decimal LowConfidence = 0.6m;

    public static TransferProposal Transfer(string from, string to, decimal officeAmount, decimal? rate,
        decimal? sourceHandlingFee, decimal? sourceWireFee, string wireFeePaidBy, PopReading? r)
    {
        var notes = new List<string>();
        var (amount, bankFee) = Principal(officeAmount, from, r, notes);

        var wire = IsCny(from) ? 0m : Math.Max(sourceWireFee ?? 0m, 0m);
        if (IsCny(from) && sourceWireFee is > 0) notes.Add("Transfers from a yuan wallet have no wire fee.");
        var share = WireFeePayer.OurShare(wireFeePaidBy);
        var ours = bankFee > 0 ? 0m : Cents(wire * share);
        if (bankFee > 0 && wire * share > 0) notes.Add("The POP shows the fee we paid, so it replaces our part of the wire fee.");
        var theirs = WalletTransferPending.Convert(Cents(wire * (1 - share)), from, to, rate);

        if (r?.ExchangeRate is > 0 and var popRate && rate is > 0 && Math.Abs(popRate - rate.Value) / rate.Value > 0.01m)
            notes.Add($"The POP shows a rate of {popRate:0.######}; the saved rate is {rate:0.######}.");

        var received = WalletTransferPending.Convert(amount, from, to, rate);
        // Rate is "1 base = rate other": a POP in the destination currency goes back through it.
        var fromIsBase = WalletTransferPending.SameCurrency(WalletTransferPending.RateBase(from, to), from);
        var check = Check(officeAmount, from, to, rate,
            rate is > 0 ? $"1 {(fromIsBase ? from : to)} = {rate:0.######} {(fromIsBase ? to : from)}" : null,
            pop => fromIsBase ? pop / rate!.Value : pop * rate!.Value,
            pop => fromIsBase ? pop / officeAmount : officeAmount / pop, r);
        return new TransferProposal(amount, rate, received, Cents(Math.Max(sourceHandlingFee ?? 0m, 0m)), bankFee, ours,
            Math.Min(theirs, received), WireFeePayer.Normalize(wireFeePaidBy) ?? WireFeePayer.Theirs, notes) { Check = check };
    }

    public static WithdrawProposal Withdraw(string walletCurrency, string currency, decimal officeAmount, decimal? rate,
        decimal? handlingFee, decimal? wireFee, string wireFeePaidBy, PopReading? r)
    {
        var notes = new List<string>();
        var same = WalletWithdrawPending.SameCurrency(currency, walletCurrency);
        if (same) rate = null;
        decimal amount, bankFee;
        // What left the wallet, in the wallet's own currency, when the POP shows it that way.
        decimal? walletDebit = r is not { Status: PopReading.Done } || same ? null
            : r.TotalDebited is > 0 && Same(r.TotalDebitedCurrency, walletCurrency) ? r.TotalDebited
            : r.Amount is > 0 && Same(r.Currency, walletCurrency)
                ? r.Amount + (Same(r.FeesCurrency ?? r.Currency, walletCurrency) ? r.Fees ?? 0m : 0m)
                : null;
        if (walletDebit is { } debit && rate is > 0)
        {
            // The extra over amount × rate is the fee (the amount itself stays the office's).
            amount = r!.Amount is > 0 && Same(r.Currency, currency) ? Math.Min(Cents(r.Amount.Value), officeAmount) : officeAmount;
            bankFee = Math.Max(0m, Cents(debit - WalletWithdrawPending.ToWallet(amount, rate)));
            Confidence(r, notes);
        }
        else
        {
            (amount, var fee) = Principal(officeAmount, currency, r, notes);
            bankFee = WalletWithdrawPending.ToWallet(fee, rate);
        }

        var wire = Math.Max(wireFee ?? 0m, 0m);
        var share = WireFeePayer.OurShare(wireFeePaidBy);
        var ours = bankFee > 0 ? 0m : Cents(wire * share);
        if (bankFee > 0 && wire * share > 0) notes.Add("The POP shows the fee we paid, so it replaces our part of the wire fee.");
        if (!same && rate == null) notes.Add($"No rate yet: enter {walletCurrency} per 1 {currency}.");

        return new WithdrawProposal(amount, rate, WalletWithdrawPending.ToWallet(amount, rate),
            Cents(Math.Max(handlingFee ?? 0m, 0m)), bankFee, ours, WireFeePayer.Normalize(wireFeePaidBy) ?? WireFeePayer.Ours, notes)
        {
            Check = PerUnitCheck(officeAmount, currency, walletCurrency, rate, r),
        };
    }

    public static DepositProposal Deposit(string walletCurrency, string currency, decimal fallbackAmount, decimal? rate, PopReading? r)
    {
        var notes = new List<string>();
        var amount = fallbackAmount;
        if (r is { Status: PopReading.Done, Amount: > 0 } && (r.Currency == null || Same(r.Currency, currency)))
        {
            amount = Cents(r.Amount.Value);
            if (r.Fees is > 0) notes.Add("Fees on the POP were paid by the customer: not counted.");
            Confidence(r, notes);
        }
        else if (r is { Status: PopReading.Done, Amount: > 0 })
            notes.Add($"The POP is in {r.Currency}, not {currency}: check the amount.");
        var same = string.Equals(currency, walletCurrency, StringComparison.OrdinalIgnoreCase);
        if (same) rate = null;
        return new DepositProposal(amount, rate, IncomingDeposit.WalletAmount(amount, rate), notes)
        {
            Check = PerUnitCheck(fallbackAmount, currency, walletCurrency, rate, r),
        };
    }

    /// <summary>Same currency: a cent either way. Converted: the rate is ours, not the bank's, so 0.5%.</summary>
    public const decimal ConvertedTolerance = 0.005m;

    /// <summary>Withdraws, supplier payments and deposits: <paramref name="rate"/> = <paramref name="other"/> per 1 <paramref name="setCurrency"/>.</summary>
    public static PopCheck? PerUnitCheck(decimal setAmount, string setCurrency, string other, decimal? rate, PopReading? r) =>
        Check(setAmount, setCurrency, other, rate, rate is > 0 ? $"{rate:0.######} {Norm(other)} per 1 {Norm(setCurrency)}" : null,
            pop => pop / rate!.Value, pop => pop / setAmount, r);

    /// <summary>The POP's amount next to the one set; a POP in <paramref name="other"/> is converted with the rate.</summary>
    private static PopCheck? Check(decimal setAmount, string setCurrency, string other, decimal? rate, string? rateText,
        Func<decimal, decimal> otherToSet, Func<decimal, decimal> popRateOf, PopReading? r)
    {
        if (r is not { Status: PopReading.Done, Amount: > 0 } || setAmount <= 0) return null;
        var pop = Cents(r.Amount.Value);
        var popCurrency = r.Currency ?? setCurrency;
        if (Same(popCurrency, setCurrency))
        {
            var diff = pop - setAmount;
            return new PopCheck(pop, Norm(popCurrency), null, null, pop, setAmount, Norm(setCurrency), diff, null, Math.Abs(diff) <= 0.01m);
        }
        if (!Same(popCurrency, other) || rate is not > 0) return null;
        var inSet = Cents(otherToSet(pop));
        var difference = inSet - setAmount;
        return new PopCheck(pop, Norm(popCurrency), rate, rateText, inSet, setAmount, Norm(setCurrency), difference,
            Math.Round(popRateOf(pop), 6, MidpointRounding.AwayFromZero),
            Math.Abs(difference) <= Math.Max(0.01m, Cents(setAmount * ConvertedTolerance)));
    }

    /// <summary>
    /// The amount sent and the fee on top, both in <paramref name="currency"/>. A POP amount above the office's is the
    /// office amount plus a fee; below it, it is what really went (and the office should look).
    /// </summary>
    private static (decimal Amount, decimal Fee) Principal(decimal officeAmount, string currency, PopReading? r, List<string> notes)
    {
        if (r is not { Status: PopReading.Done }) return (officeAmount, 0m);
        if (r.IsPaymentProof == false) notes.Add("The reader says this may not be a payment proof.");
        if (r.Amount is not > 0)
        {
            notes.Add("The reader found no amount on the POP.");
            return (officeAmount, 0m);
        }
        if (r.Currency != null && !Same(r.Currency, currency))
        {
            notes.Add($"The POP is in {r.Currency}, not {currency}: check the numbers.");
            return (officeAmount, 0m);
        }
        Confidence(r, notes);

        var popAmount = Cents(r.Amount.Value);
        var fees = r.Fees is > 0 && (r.FeesCurrency == null || Same(r.FeesCurrency, currency)) ? r.Fees.Value : 0m;
        var total = r.TotalDebited is > 0 && (r.TotalDebitedCurrency == null || Same(r.TotalDebitedCurrency, currency))
            ? r.TotalDebited.Value
            : popAmount + fees;
        total = Math.Max(total, popAmount + fees);

        var amount = Math.Min(popAmount, officeAmount);
        if (popAmount < officeAmount) notes.Add($"The POP shows {popAmount:0.00} {currency} sent, less than the {officeAmount:0.00} set.");
        return (amount, Math.Max(0m, Cents(total - amount)));
    }

    private static void Confidence(PopReading r, List<string> notes)
    {
        if (r.Confidence is { } c && c < LowConfidence) notes.Add($"The reader is not sure ({c:0.##}): check against the POP.");
    }

    private static decimal Cents(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
    private static bool Same(string? a, string? b) => a != null && b != null && Norm(a) == Norm(b);
    private static string Norm(string c) => c.Trim().ToUpperInvariant() is "RMB" or "CNH" ? "CNY" : c.Trim().ToUpperInvariant();
    private static bool IsCny(string c) => Norm(c) == "CNY";
}
