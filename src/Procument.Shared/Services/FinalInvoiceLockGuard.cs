using Microsoft.EntityFrameworkCore;

namespace Procument.Shared.Services;

/// <summary>
/// A Sales Order can be final-invoiced part by part, so documents lock only once every part is
/// on a Final Invoice — until then the remaining parts stay editable.
/// Tables: Invoices = Final Invoices, InvoiceItems = Final Invoice lines,
/// ProformaInvoices / ProformaInvoiceItems = Sales Orders and their lines.
/// </summary>
public interface IFinalInvoiceLockGuard
{
    Task<bool> IsRfqLocked(long rfqId);
    Task<bool> IsQuoteLocked(long quoteId);
    Task<bool> IsInvoiceLocked(long invoiceId);
    Task<bool> IsPurchaseOrderLocked(long poId);
    /// <summary>True as soon as any part of this Sales Order is on a Final Invoice.</summary>
    Task<bool> HasAnyFinalInvoice(long invoiceId);
    /// <summary>True when any live part of this PO (or, when given, of these PO lines) is on a Final Invoice.</summary>
    Task<bool> HasAnyFinalInvoicedPart(long poId, IReadOnlyCollection<long>? poItemIds = null);
}

public class FinalInvoiceLockGuard : IFinalInvoiceLockGuard
{
    private readonly DbContext _db;

    public FinalInvoiceLockGuard(DbContext db)
    {
        _db = db;
    }

    // A Sales Order is fully invoiced when it has a Final Invoice and no live line is left off one.
    private const string FullyInvoicedPi = """
        EXISTS (SELECT 1 FROM Invoices fi WHERE fi.ProformaInvoiceId = i.Id)
        AND NOT EXISTS (
            SELECT 1 FROM ProformaInvoiceItems ii
            WHERE ii.InvoiceId = i.Id AND ISNULL(ii.Status, '') <> 'Cancelled'
              AND NOT EXISTS (SELECT 1 FROM InvoiceItems fii WHERE fii.InvoiceItemId = ii.Id))
        """;

    /// <summary>Locked once every part of this Sales Order is on a Final Invoice.</summary>
    public async Task<bool> IsInvoiceLocked(long invoiceId)
    {
        return await _db.Database
            .SqlQueryRaw<int>($"SELECT 1 AS [Value] FROM ProformaInvoices i WHERE i.Id = {{0}} AND {FullyInvoicedPi}", invoiceId)
            .AnyAsync();
    }

    /// <summary>Locked once a Sales Order of this Quote is fully final-invoiced.</summary>
    public async Task<bool> IsQuoteLocked(long quoteId)
    {
        return await _db.Database
            .SqlQueryRaw<int>($"SELECT 1 AS [Value] FROM ProformaInvoices i WHERE i.QuoteId = {{0}} AND {FullyInvoicedPi}", quoteId)
            .AnyAsync();
    }

    /// <summary>Locked once a Sales Order of any Quote of this RFQ is fully final-invoiced.</summary>
    public async Task<bool> IsRfqLocked(long rfqId)
    {
        return await _db.Database
            .SqlQueryRaw<int>($"SELECT 1 AS [Value] FROM ProformaInvoices i INNER JOIN Quotes q ON i.QuoteId = q.Id WHERE q.RFQId = {{0}} AND {FullyInvoicedPi}", rfqId)
            .AnyAsync();
    }

    /// <summary>Locked once every live part of this PO is on a Final Invoice.</summary>
    public async Task<bool> IsPurchaseOrderLocked(long poId)
    {
        return await _db.Database
            .SqlQuery<int>($"""
                SELECT 1 AS [Value] FROM PurchaseOrders po
                WHERE po.Id = {poId}
                  AND EXISTS (
                      SELECT 1 FROM POItems pi INNER JOIN InvoiceItems fii ON fii.InvoiceItemId = pi.InvoiceItemId
                      WHERE pi.POId = po.Id AND pi.ReturnedAt IS NULL)
                  AND NOT EXISTS (
                      SELECT 1 FROM POItems pi
                      WHERE pi.POId = po.Id AND pi.ReturnedAt IS NULL AND ISNULL(pi.Status, '') <> 'Cancelled'
                        AND NOT EXISTS (SELECT 1 FROM InvoiceItems fii WHERE fii.InvoiceItemId = pi.InvoiceItemId))
                """)
            .AnyAsync();
    }

    public async Task<bool> HasAnyFinalInvoice(long invoiceId)
    {
        return await _db.Database
            .SqlQuery<int>($"SELECT 1 AS [Value] FROM Invoices WHERE ProformaInvoiceId = {invoiceId}")
            .AnyAsync();
    }

    public async Task<bool> HasAnyFinalInvoicedPart(long poId, IReadOnlyCollection<long>? poItemIds = null)
    {
        var sql = "SELECT 1 AS [Value] FROM POItems pi INNER JOIN InvoiceItems fii ON fii.InvoiceItemId = pi.InvoiceItemId WHERE pi.POId = {0} AND pi.ReturnedAt IS NULL";
        var parameters = new List<object> { poId };
        if (poItemIds is { Count: > 0 })
        {
            var ids = poItemIds.Distinct().ToList();
            sql += $" AND pi.Id IN ({string.Join(",", ids.Select((_, i) => $"{{{i + 1}}}"))})";
            parameters.AddRange(ids.Cast<object>());
        }
        return await _db.Database.SqlQueryRaw<int>(sql, parameters.ToArray()).AnyAsync();
    }
}
