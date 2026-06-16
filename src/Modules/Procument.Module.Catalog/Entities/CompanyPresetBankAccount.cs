using Procument.Shared.Entities;

namespace Procument.Module.Catalog.Entities;

public class CompanyPresetBankAccount : BaseEntity
{
    public long CompanyPresetId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string? BankName { get; set; }
    public string? BankAddress { get; set; }
    public string? AccountNumber { get; set; }
    public string? BeneficiaryName { get; set; }
    public string? SwiftCode { get; set; }
    public int SortOrder { get; set; } = 0;

    public CompanyPreset CompanyPreset { get; set; } = null!;
}
