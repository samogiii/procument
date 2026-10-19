namespace Procument.Module.Sales.Entities;

/// <summary>
/// Global wallet settings — a single row (Id = 1). A missing row means every switch is off.
/// </summary>
public class WalletSettings
{
    public const long SingletonId = 1;

    public long Id { get; set; } = SingletonId;
    /// <summary>
    /// When on, system-created deposits and withdraws (POPs, bank fees, supplier credits) count in
    /// their wallet. When off they are still recorded but kept out of every wallet — see WalletImport.
    /// </summary>
    public bool AutoImport { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? UpdatedByUserId { get; set; }
}
