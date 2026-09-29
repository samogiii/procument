using Procument.Module.Catalog.Entities;

namespace Procument.Module.Sales.Entities;

/// <summary>
/// Credit we hold with one supplier under one of our companies. The same supplier can have
/// separate balances per company preset; a PO may only use the balance of its own company.
/// </summary>
public class SupplierCreditAccount
{
    public long Id { get; set; }
    public long SupplierId { get; set; }
    public long CompanyPresetId { get; set; }
    public string Currency { get; set; } = "USD";
    /// <summary>
    /// <see cref="SupplierCreditAccountKinds.Prepaid"/>: credit the supplier holds for us.
    /// <see cref="SupplierCreditAccountKinds.CreditLine"/>: buy now, pay back later — Balance is what is
    /// still available, CreditLimit − Balance is what we owe the supplier.
    /// </summary>
    public string Kind { get; set; } = SupplierCreditAccountKinds.Prepaid;
    /// <summary>Credit lines only: the limit the supplier granted (moved by CreditLimit entries).</summary>
    public decimal? CreditLimit { get; set; }
    /// <summary>Cached running balance; SupplierCreditEntries is the source of truth.</summary>
    public decimal Balance { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ModifyAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public Supplier Supplier { get; set; } = null!;
    public CompanyPreset CompanyPreset { get; set; } = null!;
    public ICollection<SupplierCreditEntry> Entries { get; set; } = [];

    /// <summary>Credit lines: what we owe the supplier. Prepaid accounts owe nothing.</summary>
    public decimal Owed => Kind == SupplierCreditAccountKinds.CreditLine ? (CreditLimit ?? 0) - Balance : 0;
}

public static class SupplierCreditAccountKinds
{
    public const string Prepaid = "Prepaid";
    public const string CreditLine = "CreditLine";
}
