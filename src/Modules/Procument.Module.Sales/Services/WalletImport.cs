using Microsoft.EntityFrameworkCore;
using Procument.Module.Sales.Entities;

namespace Procument.Module.Sales.Services;

/// <summary>
/// The Wallets page "Auto Import" switch. System-created wallet rows (Customer POP deposits, supplier
/// POP withdraws and their bank fees, supplier-credit top-ups/repayments) are always written, because
/// PI/PO paid totals and supplier credits are built from them. With the switch off they are written with
/// <see cref="PaymentTransaction.InWallet"/> = false, so no wallet balance or ledger shows them.
/// </summary>
public static class WalletImport
{
    /// <summary>A missing settings row means the switch is off.</summary>
    public static Task<bool> IsOnAsync(DbContext db) =>
        db.Set<WalletSettings>().AsNoTracking()
            .Where(s => s.Id == WalletSettings.SingletonId)
            .Select(s => s.AutoImport)
            .FirstOrDefaultAsync();
}
