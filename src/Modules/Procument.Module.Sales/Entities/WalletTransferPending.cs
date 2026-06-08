namespace Procument.Module.Sales.Entities;

public class WalletTransferPending
{
    public long Id { get; set; }
    public long FromBoxId { get; set; }
    public long ToBoxId { get; set; }
    public decimal WithdrawAmount { get; set; }
    public decimal DepositAmount { get; set; }
    public decimal? ExchangeRate { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "Pending"; // Pending | Accepted | Rejected | Completed
    public string? PopFileName { get; set; }
    public string? RejectionNote { get; set; }
    public long CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long? AcceptedByUserId { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public long? CompletedByUserId { get; set; }
    public DateTime? CompletedAt { get; set; }

    public PaymentBox FromBox { get; set; } = null!;
    public PaymentBox ToBox { get; set; } = null!;
}
