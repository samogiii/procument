using Procument.Module.Catalog.Entities;

namespace Procument.Module.Sales.Entities;

public class PaymentBox
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public long CompanyPresetId { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Set = this is a sub-wallet of that main wallet; null = a main wallet. Only changes how wallets
    /// are grouped on the Wallets page: transactions stay on the wallet they were posted to.
    /// </summary>
    public long? ParentBoxId { get; set; }

    /// <summary>The name the public warehouse app sees instead of Name. Null = no surname, Name is sent.</summary>
    public string? Surname { get; set; }
    /// <summary>This wallet's fixed bank charge, in its currency; fills the phone's fee box. Null = not fixed.</summary>
    public decimal? HandlingFee { get; set; }
    /// <summary>This wallet's fixed wire charge, in its currency; fills the phone's fee box. Null = not fixed.</summary>
    public decimal? WireFee { get; set; }

    // Bank details — override the company preset's bank details when set
    public string? BankName { get; set; }
    public string? BankAddress { get; set; }
    public string? AccountNumber { get; set; }
    public string? BeneficiaryName { get; set; }
    public string? SwiftCode { get; set; }

    public CompanyPreset CompanyPreset { get; set; } = null!;
    public PaymentBox? ParentBox { get; set; }
    public ICollection<PaymentBox> SubWallets { get; set; } = [];
    public ICollection<PaymentTransaction> Transactions { get; set; } = [];

    /// <summary>
    /// What the warehouse app may see of a wallet: it runs on a public server, so a wallet with a surname
    /// sends only that. Its company is left out too, because the company identifies the wallet just as well.
    /// </summary>
    public static (string Name, string? Company) ForWarehouse(string name, string? surname, string? company) =>
        string.IsNullOrWhiteSpace(surname) ? (name, company) : (surname.Trim(), null);
}
