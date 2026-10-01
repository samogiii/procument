using Microsoft.EntityFrameworkCore;
using Procument.Module.Identity.Entities;

namespace Procument.Module.Purchasing.Services;

/// <summary>
/// Which customer bases a user may see on the Shipping pages. Granted per user in Menu Access
/// as the features <c>shippingBase1</c> … <c>shippingBase5</c> — no role grants them and
/// SuperAdmin does not bypass. A line with no customer (e.g. a Stock PO) counts as Base 1.
///
/// Base 1 shipments carry a T-ID and SO#. Bases 2–5 ship without them and can instead be
/// collected by the customer at the warehouse (PickUp).
/// </summary>
public static class ShippingBaseAccess
{
    public const int MaxBase = 5;
    public const string FeaturePrefix = "shippingBase";

    public static IEnumerable<string> Features => Enumerable.Range(1, MaxBase).Select(b => FeaturePrefix + b);

    /// <summary>The base a line is shown and filtered under. No customer → Base 1.</summary>
    public static int Effective(int? customerBase) => customerBase ?? 1;

    /// <summary>Base 1 shipments carry T-ID / SO#.</summary>
    public static bool UsesTidSo(int? customerBase) => Effective(customerBase) == 1;

    /// <summary>Bases 2–5 may be picked up by the customer at the warehouse.</summary>
    public static bool CanPickUp(int? customerBase) => Effective(customerBase) >= 2;

    /// <summary>The bases granted to this user in Menu Access. Empty when none.</summary>
    public static async Task<HashSet<int>> LoadAsync(DbContext db, string? userName)
    {
        if (string.IsNullOrWhiteSpace(userName)) return [];
        var features = await db.Set<MenuPermission>().AsNoTracking()
            .Where(p => p.UserName == userName && p.Feature.StartsWith(FeaturePrefix))
            .Select(p => p.Feature)
            .ToListAsync();
        return features
            .Select(f => int.TryParse(f[FeaturePrefix.Length..], out var b) ? b : 0)
            .Where(b => b >= 1 && b <= MaxBase)
            .ToHashSet();
    }
}
