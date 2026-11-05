using Procument.Module.Identity.Entities;

namespace Procument.Module.Sales.Entities;

/// <summary>
/// Money a Customer POP says is coming. The POP pays the PI at once, but the wallet only moves when the
/// warehouse has confirmed what reached the bank and the office accepts it (Wallets, Incoming Money tab).
/// Bank fee = OfficeAmount − BankWalletAmount. Only written where a warehouse is configured.
/// </summary>
public class IncomingDeposit
{
    public const string Pending = "Pending";
    public const string Accepted = "Accepted";
    public const string CustomerPopFolder = "Customer POP";
    public const string BankPopFolder = "Bank POP";

    public long Id { get; set; }
    /// <summary>Identifies this deposit on the warehouse sync; becomes the wallet row's PopUploadId.</summary>
    public Guid UploadId { get; set; } = Guid.NewGuid();
    public long CustomerPaymentId { get; set; }
    public long PaymentBoxId { get; set; }

    /// <summary>The POP's price, in Currency.</summary>
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    /// <summary>Wallet currency per 1 Currency; null when the POP is already in the wallet's currency.</summary>
    public decimal? WalletRate { get; set; }
    /// <summary>Procument side: Amount × WalletRate, wallet currency.</summary>
    public decimal OfficeAmount { get; set; }

    // ── Confirmed in the warehouse ──
    public string? BankPopFileName { get; set; }
    /// <summary>The price on the bank POP, in Currency.</summary>
    public decimal? BankAmount { get; set; }
    public decimal? BankRate { get; set; }
    /// <summary>The Bank of China rate behind BankRate (before +0.2) and when BOC published it; null = not from BOC.</summary>
    public decimal? BocRate { get; set; }
    public string? RateAt { get; set; }
    /// <summary>Warehouse side: BankAmount × BankRate, wallet currency.</summary>
    public decimal? BankWalletAmount { get; set; }
    public string? BankBy { get; set; }
    public DateTime? BankAt { get; set; }
    /// <summary>Why the office sent the warehouse's result back.</summary>
    public string? RejectNote { get; set; }

    // ── Accepted in Wallets ──
    public string Status { get; set; } = Pending;
    public decimal? BankFee { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public long? ReviewedByUserId { get; set; }
    public long? DepositTransactionId { get; set; }
    public long? FeeTransactionId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public CustomerPayment CustomerPayment { get; set; } = null!;
    public PaymentBox PaymentBox { get; set; } = null!;
    public User? ReviewedBy { get; set; }
    public PaymentTransaction? DepositTransaction { get; set; }
    public PaymentTransaction? FeeTransaction { get; set; }

    /// <summary>Procument − warehouse, both in the wallet currency. Negative: more arrived than expected.</summary>
    public static decimal Difference(decimal officeAmount, decimal bankWalletAmount) => officeAmount - bankWalletAmount;

    public static decimal WalletAmount(decimal amount, decimal? rate)
        => Math.Round(amount * (rate ?? 1m), 2, MidpointRounding.AwayFromZero);

    /// <summary>What a Customer POP line shows about its money.</summary>
    public static string Stage(string status, DateTime? bankAt) =>
        status == Accepted ? "In wallet" : bankAt != null ? "Waiting for acceptance in Wallets" : "Waiting for the warehouse";
}
