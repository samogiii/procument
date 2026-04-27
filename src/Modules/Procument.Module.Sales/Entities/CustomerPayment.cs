namespace Procument.Module.Sales.Entities;

public class CustomerPayment
{
    public long Id { get; set; }
    public long InvoiceId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Invoice Invoice { get; set; } = null!;
}
