namespace Procument.Module.Sales.Entities;

/// <summary>
/// A future withdraw the wallet admin expects to pay (always USD). Reminder only —
/// never touches any wallet balance or ledger.
/// </summary>
public class UpcomingWithdraw
{
    public long Id { get; set; }
    public decimal Price { get; set; }
    public string Description { get; set; } = string.Empty;
    public long CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
