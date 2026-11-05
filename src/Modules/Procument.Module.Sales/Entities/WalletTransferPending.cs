namespace Procument.Module.Sales.Entities;

public class WalletTransferPending
{
    public long Id { get; set; }
    public long FromBoxId { get; set; }
    public long ToBoxId { get; set; }
    public decimal WithdrawAmount { get; set; }
    /// <summary>What the transfer converts to in the destination currency, before the wire fee.</summary>
    public decimal DepositAmount { get; set; }
    /// <summary>With RateBaseCurrency: 1 RateBaseCurrency = rate (the other currency). Without it (old rows): 1 From = rate To.</summary>
    public decimal? ExchangeRate { get; set; }
    public string? RateBaseCurrency { get; set; }
    /// <summary>Bank handling charge, From currency: taken from the source wallet on top of WithdrawAmount.</summary>
    public decimal? HandlingFee { get; set; }
    /// <summary>The destination's part of the wire fee, To currency: taken from what reaches the destination wallet.</summary>
    public decimal? WireFee { get; set; }
    /// <summary>The source's part of the wire fee, From currency (WireFeePaidBy Ours / Split).</summary>
    public decimal? SourceWireFee { get; set; }
    /// <summary>What the POP shows was taken on top of the amount (bank / wire fee on the POP), From currency.</summary>
    public decimal? BankFee { get; set; }
    /// <summary>WireFeePayer, chosen when the transfer was made (the source wallet's setting by default).</summary>
    public string? WireFeePaidBy { get; set; }
    public string? Notes { get; set; }
    /// <summary>Accepted (waiting for the POP) → Review (POP in, the office checks the POP reader's numbers) → Completed.</summary>
    public string Status { get; set; } = "Accepted";
    /// <summary>The Bank of China rate behind the warehouse's rate (before +0.2) and when BOC published it.</summary>
    public decimal? BocRate { get; set; }
    public string? RateAt { get; set; }
    /// <summary>When the POP reached the office (warehouse sync or uploaded here).</summary>
    public DateTime? PopAt { get; set; }
    public string? PopFileName { get; set; }
    public string? RejectionNote { get; set; }
    public long CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long? AcceptedByUserId { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public long? CompletedByUserId { get; set; }
    public DateTime? CompletedAt { get; set; }
    /// <summary>Who did it on the warehouse phone; set = completed by the warehouse sync.</summary>
    public string? WarehouseBy { get; set; }

    public PaymentBox FromBox { get; set; } = null!;
    public PaymentBox ToBox { get; set; } = null!;

    // The warehouse app (TransferMath there) works these out the same way.

    public static bool SameCurrency(string from, string to) => string.Equals(from, to, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The rate is "1 base = rate other": dollars when either wallet is in dollars (6.91 = $1 → ¥6.91, either way),
    /// otherwise the source currency.
    /// </summary>
    public static string RateBase(string from, string to) => IsUsd(to) && !IsUsd(from) ? to : from;

    /// <summary>Amount (From currency) in the To currency, rounded to cents.</summary>
    public static decimal Convert(decimal amount, string from, string to, decimal? rate)
    {
        if (SameCurrency(from, to) || rate is not > 0) return amount;
        var value = SameCurrency(RateBase(from, to), from) ? amount * rate.Value : amount / rate.Value;
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    public string? RateText() => ExchangeRate is not { } rate ? null
        : RateBaseCurrency is { } b ? $"1 {b} = {rate:0.######} {(SameCurrency(b, FromBox.Currency) ? ToBox.Currency : FromBox.Currency)}"
        : $"1 {FromBox.Currency} = {rate:0.######} {ToBox.Currency}";

    private static bool IsUsd(string c) => string.Equals(c, "USD", StringComparison.OrdinalIgnoreCase);
}
