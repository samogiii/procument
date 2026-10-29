using Microsoft.EntityFrameworkCore;
using Procument.Module.Catalog.Entities;
using Procument.Module.Purchasing.Entities;
using Procument.Module.Sales.Entities;

namespace Procument.Module.Sales.Services;

/// <summary>
/// Wallet transaction Base tags. <see cref="PaymentTransaction.Base"/> holds one or more of "B1".."B7",
/// comma-separated and sorted ("B1,B3"). They are filled from the customers' bases: a customer POP from
/// its customer, a PR from every PI customer its PO lines belong to (a merged PO can serve several).
/// </summary>
public static class TransactionBases
{
    /// <summary>Keeps only "B1".."B7", de-duplicated and sorted; null when none are left.</summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var tags = value.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(v => v.Trim().ToUpperInvariant())
            .Where(v => v.Length == 2 && v[0] == 'B' && v[1] >= '1' && v[1] <= '7')
            .Distinct()
            .OrderBy(v => v)
            .ToList();
        return tags.Count == 0 ? null : string.Join(",", tags);
    }

    public static bool Has(string? bases, string tag) =>
        !string.IsNullOrEmpty(bases) && bases.Split(',').Contains(tag);

    private static string? FromCustomerBases(IEnumerable<int?> bases) =>
        Normalize(string.Join(",", bases.Where(b => b.HasValue).Select(b => $"B{b}")));

    public static async Task<string?> ForCustomerAsync(DbContext db, long customerId) =>
        FromCustomerBases(await db.Set<Customer>().AsNoTracking()
            .Where(c => c.Id == customerId)
            .Select(c => c.Base)
            .ToListAsync());

    public static async Task<string?> ForInvoiceAsync(DbContext db, long invoiceId) =>
        FromCustomerBases(await db.Set<Invoice>().AsNoTracking()
            .Where(i => i.Id == invoiceId)
            .Select(i => i.Customer.Base)
            .ToListAsync());

    /// <summary>The bases of every PI customer behind the PR's PO: its lines' PIs plus the PO's own PI.</summary>
    public static async Task<string?> ForPaymentRequestAsync(DbContext db, long paymentRequestId)
    {
        var po = await db.Set<PaymentRequest>().AsNoTracking()
            .Where(pr => pr.Id == paymentRequestId && pr.POId != null)
            .Select(pr => new { POId = pr.POId!.Value, pr.PO!.InvoiceId })
            .FirstOrDefaultAsync();
        if (po == null) return null;

        var invoiceItemIds = db.Set<POItem>()
            .Where(i => i.POId == po.POId && i.ReturnedAt == null && i.InvoiceItemId != null)
            .Select(i => i.InvoiceItemId!.Value);
        var invoiceIds = await db.Set<InvoiceItem>().AsNoTracking()
            .Where(ii => invoiceItemIds.Contains(ii.Id))
            .Select(ii => ii.InvoiceId)
            .Distinct()
            .ToListAsync();
        if (po.InvoiceId.HasValue) invoiceIds.Add(po.InvoiceId.Value);
        if (invoiceIds.Count == 0) return null;

        return FromCustomerBases(await db.Set<Invoice>().AsNoTracking()
            .Where(i => invoiceIds.Contains(i.Id))
            .Select(i => i.Customer.Base)
            .ToListAsync());
    }

    /// <summary>What a new row should be tagged with: the PR's customers, else the PI's, else the customer's.</summary>
    public static async Task<string?> SuggestAsync(DbContext db, long? paymentRequestId, long? invoiceId, long? customerId)
    {
        if (paymentRequestId.HasValue && await ForPaymentRequestAsync(db, paymentRequestId.Value) is { } fromPr) return fromPr;
        if (invoiceId.HasValue && await ForInvoiceAsync(db, invoiceId.Value) is { } fromPi) return fromPi;
        if (customerId.HasValue) return await ForCustomerAsync(db, customerId.Value);
        return null;
    }
}
