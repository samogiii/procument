using Procument.Module.Identity.Entities;

namespace Procument.Module.Sales.Entities;

/// <summary>
/// A shipment's cost entered in the warehouse app with its shipping documents (CNY). It reaches the
/// office on the Payment sync and waits on the Payment page, where accepting it withdraws it from the
/// wallet the user picks. One row per warehouse shipment; a rejected cost fixed on the phone reuses it.
/// </summary>
public class WarehouseShippingCost
{
    public const string Pending = "Pending";
    public const string Accepted = "Accepted";
    public const string Rejected = "Rejected";

    public long Id { get; set; }
    public long WarehouseShipmentId { get; set; }
    /// <summary>The phone's edit counter: a resend of a version we already have is ignored.</summary>
    public int Version { get; set; }

    public string TrackNumber { get; set; } = "";
    public string? Carrier { get; set; }
    public string? WarehouseName { get; set; }
    /// <summary>The shipment's POItemTrackNumber ids, comma-separated.</summary>
    public string TrackIds { get; set; } = "";

    public decimal FixedCost { get; set; }
    public decimal OtherCost { get; set; }
    public string? EnteredBy { get; set; }
    public DateTime EnteredAt { get; set; }

    public string Status { get; set; } = Pending;
    public DateTime? ReviewedAt { get; set; }
    public long? ReviewedByUserId { get; set; }
    public string? RejectNote { get; set; }

    // Accepted: where the money came from.
    public long? PaymentBoxId { get; set; }
    /// <summary>CNY per 1 unit of the wallet currency; null for CNY wallets.</summary>
    public decimal? ExchangeRate { get; set; }
    public decimal? WalletAmount { get; set; }
    public long? PaymentTransactionId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public PaymentBox? PaymentBox { get; set; }
    public PaymentTransaction? PaymentTransaction { get; set; }
    public User? ReviewedBy { get; set; }
}
