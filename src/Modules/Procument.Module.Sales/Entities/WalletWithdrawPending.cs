using Procument.Module.Catalog.Entities;

namespace Procument.Module.Sales.Entities;

/// <summary>
/// A wallet withdraw the warehouse pays (Wallets → Ready To Withdraw). Nothing moves until the warehouse phone sends
/// the POP, rate, handling fee and wire fee; the next Payment sync then writes the withdraw row and a bank fee row.
/// </summary>
public class WalletWithdrawPending
{
    public const string Pending = "Pending";
    /// <summary>The POP is in; the office checks the POP reader's numbers and accepts.</summary>
    public const string Review = "Review";
    public const string Completed = "Completed";

    public long Id { get; set; }
    public long PaymentBoxId { get; set; }
    /// <summary>In Currency (null = the wallet's own currency).</summary>
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    /// <summary>Wallet currency per 1 Currency as the office expects it; optional.</summary>
    public decimal? OfficeRate { get; set; }
    /// <summary>"MotherWallet" | "Supplier", as on a manual withdraw.</summary>
    public string ToType { get; set; } = "MotherWallet";
    public long? ToSupplierId { get; set; }
    public long? PaymentRequestId { get; set; }
    public string? Base { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = Pending;
    public long CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── From the warehouse ──
    public string? PopFileName { get; set; }
    /// <summary>Wallet currency per 1 Currency; null when they match.</summary>
    public decimal? ExchangeRate { get; set; }
    /// <summary>The Bank of China rate behind ExchangeRate (before +0.2) and when BOC published it.</summary>
    public decimal? BocRate { get; set; }
    public string? RateAt { get; set; }
    public DateTime? PopAt { get; set; }
    /// <summary>Wallet currency. HandlingFee is the wallet's own; WireFee is our part of it (WireFeePaidBy).</summary>
    public decimal? HandlingFee { get; set; }
    public decimal? WireFee { get; set; }
    /// <summary>What the POP shows was taken on top of the amount (bank / wire fee on the POP), wallet currency.</summary>
    public decimal? BankFee { get; set; }
    /// <summary>WireFeePayer, chosen when the withdraw was made (the wallet's setting by default).</summary>
    public string? WireFeePaidBy { get; set; }
    /// <summary>Amount × rate, wallet currency, fees not included.</summary>
    public decimal? WalletAmount { get; set; }
    public string? WarehouseBy { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long? TransactionId { get; set; }
    public long? FeeTransactionId { get; set; }

    public PaymentBox PaymentBox { get; set; } = null!;
    public Supplier? ToSupplier { get; set; }

    public static bool SameCurrency(string? currency, string walletCurrency)
        => currency == null || string.Equals(currency, walletCurrency, StringComparison.OrdinalIgnoreCase);

    /// <summary>Amount × rate in the wallet currency, rounded like the payment flow.</summary>
    public static decimal ToWallet(decimal amount, decimal? rate)
        => Math.Round(amount * (rate ?? 1m), 2, MidpointRounding.AwayFromZero);
}
