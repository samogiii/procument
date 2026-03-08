using Microsoft.EntityFrameworkCore;

namespace Procument.Shared.Services;

public interface IFinalInvoiceLockGuard
{
    Task<bool> IsRfqLocked(long rfqId);
    Task<bool> IsQuoteLocked(long quoteId);
    Task<bool> IsInvoiceLocked(long invoiceId);
    Task<bool> IsPurchaseOrderLocked(long poId);
}

public class FinalInvoiceLockGuard : IFinalInvoiceLockGuard
{
    private readonly DbContext _db;

    public FinalInvoiceLockGuard(DbContext db)
    {
        _db = db;
    }

    /// <summary>Check if any FinalInvoice exists for this Invoice (Proforma Invoice) ID.</summary>
    public async Task<bool> IsInvoiceLocked(long invoiceId)
    {
        return await _db.Database
            .SqlQuery<int>($"SELECT 1 AS [Value] FROM FinalInvoices WHERE ProformaInvoiceId = {invoiceId}")
            .AnyAsync();
    }

    /// <summary>Check if any FinalInvoice exists for any Invoice linked to this Quote.</summary>
    public async Task<bool> IsQuoteLocked(long quoteId)
    {
        return await _db.Database
            .SqlQuery<int>($"SELECT 1 AS [Value] FROM FinalInvoices fi INNER JOIN Invoices i ON fi.ProformaInvoiceId = i.Id WHERE i.QuoteId = {quoteId}")
            .AnyAsync();
    }

    /// <summary>Check if any FinalInvoice exists for any Invoice linked to any Quote of this RFQ.</summary>
    public async Task<bool> IsRfqLocked(long rfqId)
    {
        return await _db.Database
            .SqlQuery<int>($"SELECT 1 AS [Value] FROM FinalInvoices fi INNER JOIN Invoices i ON fi.ProformaInvoiceId = i.Id INNER JOIN Quotes q ON i.QuoteId = q.Id WHERE q.RFQId = {rfqId}")
            .AnyAsync();
    }

    /// <summary>Check if any FinalInvoice exists for the Invoice linked to this PO.</summary>
    public async Task<bool> IsPurchaseOrderLocked(long poId)
    {
        return await _db.Database
            .SqlQuery<int>($"SELECT 1 AS [Value] FROM FinalInvoices fi INNER JOIN PurchaseOrders po ON fi.ProformaInvoiceId = po.InvoiceId WHERE po.Id = {poId}")
            .AnyAsync();
    }
}
