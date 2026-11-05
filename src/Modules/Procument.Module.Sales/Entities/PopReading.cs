using Microsoft.EntityFrameworkCore;

namespace Procument.Module.Sales.Entities;

/// <summary>
/// What the local AI (llama-server, PopReader config) read off a POP from the warehouse: one row per deposit, transfer
/// or withdraw (Kind + RefId), read again whenever a new POP arrives. The office sees it, with the amounts worked out from
/// it (PopFees), when it accepts. Nothing here moves money by itself.
/// </summary>
public class PopReading
{
    public const string Deposit = "Deposit";
    public const string Transfer = "Transfer";
    public const string Withdraw = "Withdraw";
    /// <summary>A supplier payment paid in the warehouse (RefId = PaymentRequest id).</summary>
    public const string Payment = "Payment";

    public const string Waiting = "Waiting";
    public const string Done = "Done";
    public const string Failed = "Failed";
    /// <summary>No PopReader:Url configured.</summary>
    public const string Off = "Off";

    public const int MaxAttempts = 3;

    public long Id { get; set; }
    public string Kind { get; set; } = string.Empty;
    /// <summary>IncomingDeposit / WalletTransferPending / WalletWithdrawPending id.</summary>
    public long RefId { get; set; }
    public string Status { get; set; } = Waiting;
    public int Attempts { get; set; }

    // ── Read off the POP ──
    public bool? IsPaymentProof { get; set; }
    /// <summary>The principal: what was sent (or, for a deposit, what the customer sent), fees not included.</summary>
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    /// <summary>Charges on the POP paid by the sender (us, for a transfer or withdraw), in FeesCurrency.</summary>
    public decimal? Fees { get; set; }
    public string? FeesCurrency { get; set; }
    /// <summary>Everything taken from the sender's account, when the POP shows it.</summary>
    public decimal? TotalDebited { get; set; }
    public string? TotalDebitedCurrency { get; set; }
    public decimal? ReceivedAmount { get; set; }
    public string? ReceivedCurrency { get; set; }
    public decimal? ExchangeRate { get; set; }
    /// <summary>OUR / SHA / BEN when the POP shows it.</summary>
    public string? ChargesOption { get; set; }
    public string? ValueDate { get; set; }
    public string? Reference { get; set; }
    /// <summary>0..1, the model's own guess.</summary>
    public decimal? Confidence { get; set; }
    public string? Note { get; set; }
    /// <summary>The model's JSON answer as it came.</summary>
    public string? RawJson { get; set; }
    public string? Error { get; set; }
    public string? Model { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }

    /// <summary>A new POP arrived: (re)queue it for reading. Saved with the caller's SaveChanges.</summary>
    public static async Task QueueAsync(DbContext db, string kind, long refId)
    {
        var row = await db.Set<PopReading>().FirstOrDefaultAsync(r => r.Kind == kind && r.RefId == refId);
        if (row == null) db.Set<PopReading>().Add(row = new PopReading { Kind = kind, RefId = refId });
        row.Reset();
    }

    public void Reset()
    {
        Status = Waiting;
        Attempts = 0;
        IsPaymentProof = null;
        Amount = Fees = TotalDebited = ReceivedAmount = ExchangeRate = Confidence = null;
        Currency = FeesCurrency = TotalDebitedCurrency = ReceivedCurrency = ChargesOption = ValueDate = Reference = null;
        Note = RawJson = Error = Model = null;
        ReadAt = null;
        CreatedAt = DateTime.UtcNow;
    }
}
