using Microsoft.EntityFrameworkCore;
using Procument.Module.Sales.Entities;

namespace Procument.Module.Sales.Services;

/// <summary>
/// The single definition of "how much have we paid the supplier" on a payment request or PO:
/// wallet payments (Withdraw → Supplier) plus net supplier credit applied (Out − In).
/// Bank fees and credit top-ups (ToType = "SupplierCredit") are never payments against a PO.
/// OurInventory's StockPurchaseOrderService mirrors this rule in raw SQL — keep them in step.
/// </summary>
public static class SupplierPaymentLedger
{
    public static IQueryable<PaymentTransaction> WalletPayments(DbContext db) =>
        db.Set<PaymentTransaction>().Where(t => t.Type == "Withdraw" && t.ToType == "Supplier");

    /// <summary>Only PO applications and their reversals carry a PaymentRequestId.</summary>
    public static IQueryable<SupplierCreditEntry> CreditApplications(DbContext db) =>
        db.Set<SupplierCreditEntry>().Where(e => e.PaymentRequestId != null);

    public static async Task<decimal> PaidByPoAsync(DbContext db, long poId)
    {
        var wallet = await WalletPayments(db)
            .Where(t => t.PaymentRequest!.POId == poId)
            .SumAsync(t => t.Amount);
        var credit = await CreditApplications(db)
            .Where(e => e.PaymentRequest!.POId == poId)
            .SumAsync(e => e.Direction == SupplierCreditDirections.Out ? e.Amount : -e.Amount);
        return wallet + credit;
    }

    public static async Task<decimal> PaidByPrAsync(DbContext db, long paymentRequestId) =>
        (await PaidByPrAsync(db, [paymentRequestId])).GetValueOrDefault(paymentRequestId);

    public static async Task<Dictionary<long, decimal>> PaidByPrAsync(DbContext db, IEnumerable<long> paymentRequestIds)
    {
        var ids = paymentRequestIds.Distinct().ToArray();
        if (ids.Length == 0) return [];

        var paid = await WalletPayments(db)
            .Where(t => t.PaymentRequestId.HasValue && ids.Contains(t.PaymentRequestId.Value))
            .GroupBy(t => t.PaymentRequestId!.Value)
            .Select(g => new { Id = g.Key, Paid = g.Sum(t => t.Amount) })
            .ToDictionaryAsync(x => x.Id, x => x.Paid);

        foreach (var (id, credit) in await CreditAppliedByPrAsync(db, ids))
            paid[id] = paid.GetValueOrDefault(id) + credit;
        return paid;
    }

    public static async Task<Dictionary<long, decimal>> CreditAppliedByPrAsync(DbContext db, IEnumerable<long> paymentRequestIds)
    {
        var ids = paymentRequestIds.Distinct().ToArray();
        if (ids.Length == 0) return [];

        return await CreditApplications(db)
            .Where(e => ids.Contains(e.PaymentRequestId!.Value))
            .GroupBy(e => e.PaymentRequestId!.Value)
            .Select(g => new
            {
                Id = g.Key,
                Credit = g.Sum(e => e.Direction == SupplierCreditDirections.Out ? e.Amount : -e.Amount),
            })
            .ToDictionaryAsync(x => x.Id, x => x.Credit);
    }
}
