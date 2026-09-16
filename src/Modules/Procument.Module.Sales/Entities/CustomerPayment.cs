namespace Procument.Module.Sales.Entities;

public class CustomerPayment
{
    public long Id { get; set; }
    public long InvoiceId { get; set; }
    public string FileName { get; set; } = string.Empty;
    /// <summary>USD value applied to the PI and shown on Payment Deposit.</summary>
    public decimal Amount { get; set; }
    /// <summary>Amount shown on the customer's POP in Currency.</summary>
    public decimal? ReceivedAmount { get; set; }
    public string? Currency { get; set; }
    /// <summary>The USD/payment-to-wallet rate entered with the POP.</summary>
    public decimal? ExchangeRate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Invoice Invoice { get; set; } = null!;
}
