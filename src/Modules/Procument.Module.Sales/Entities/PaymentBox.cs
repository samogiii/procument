using Procument.Module.Catalog.Entities;

namespace Procument.Module.Sales.Entities;

public class PaymentBox
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public long CompanyPresetId { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Bank details — override the company preset's bank details when set
    public string? BankName { get; set; }
    public string? BankAddress { get; set; }
    public string? AccountNumber { get; set; }
    public string? BeneficiaryName { get; set; }
    public string? SwiftCode { get; set; }

    public CompanyPreset CompanyPreset { get; set; } = null!;
    public ICollection<PaymentTransaction> Transactions { get; set; } = [];
}
