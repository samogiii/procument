using Microsoft.EntityFrameworkCore;
using Procument.Module.Purchasing.Entities;
using Procument.Module.Purchasing.Services;

namespace Procument.Module.Sales.Services;

public sealed record SupplierPaymentProgress(decimal PoTotal, decimal PoPaid, decimal PrPaid, bool Complete)
{
    public decimal PoRemaining => Math.Max(0, PoTotal - PoPaid);
}

/// <summary>
/// Moves a payment request and its PO forward after money was recorded against them — a wallet
/// payment's final bank amount or a supplier-credit application — and back again when a credit
/// application is reversed. Call it after the payment row is saved (the paid totals are read from
/// the database). <paramref name="po"/> must have ImportDetail loaded.
/// </summary>
public static class SupplierPaymentCompletion
{
    /// <summary>PR status before any money is recorded (set by PaymentRequestService.CreateAsync).</summary>
    public const string UnpaidPrStatus = "PENDING APPROVAL";

    public static async Task<SupplierPaymentProgress> ProgressAsync(DbContext db, PurchaseOrder po, PaymentRequest pr)
    {
        var poTotal = SupplierPaymentAmounts.Total(po);
        var poPaid = await SupplierPaymentLedger.PaidByPoAsync(db, po.Id);
        var prPaid = await SupplierPaymentLedger.PaidByPrAsync(db, pr.Id);
        return new SupplierPaymentProgress(poTotal, poPaid, prPaid, poPaid >= poTotal);
    }

    public static async Task<SupplierPaymentProgress> ApplyAsync(DbContext db, PurchaseOrder po, PaymentRequest pr, long? userId)
    {
        var progress = await ProgressAsync(db, po, pr);
        pr.Status = progress.PrPaid >= (pr.Amount ?? progress.PoTotal) ? "PAID" : "PARTIALLY PAID";

        po.PaymentStatus = progress.Complete ? "Submitted" : "PartiallyPaid";
        if (progress.Complete)
        {
            po.PaymentSubmittedAt = DateTime.UtcNow;
            po.PaymentSubmittedBy = userId;
            await PurchaseOrderStatusFlow.ApplyAsync(db, po, PurchaseOrderStatusFlow.PaymentDone);
        }

        return progress;
    }

    /// <summary>
    /// After money was taken back (a reversed credit application): PR back to partially paid or
    /// unpaid, and a PO that was complete back to Waiting For Payment. The caller must first check
    /// the PO has not moved past payment (see <see cref="CanReopen"/>).
    /// </summary>
    public static async Task<SupplierPaymentProgress> ReopenAsync(DbContext db, PurchaseOrder po, PaymentRequest pr)
    {
        var progress = await ProgressAsync(db, po, pr);
        pr.Status = progress.PrPaid >= (pr.Amount ?? progress.PoTotal) ? "PAID"
            : progress.PrPaid > 0 ? "PARTIALLY PAID"
            : UnpaidPrStatus;

        if (progress.Complete) return progress;
        po.PaymentStatus = progress.PoPaid > 0 ? "PartiallyPaid" : "NotStarted";
        po.PaymentSubmittedAt = null;
        po.PaymentSubmittedBy = null;
        if (po.Status == PurchaseOrderStatusFlow.PaymentDone)
            await PurchaseOrderStatusFlow.ApplyAsync(db, po, PurchaseOrderStatusFlow.WaitingForPayment);
        return progress;
    }

    /// <summary>
    /// Whether a PO's payment may be re-opened. Past payment (shipping, received, completed) money
    /// can no longer be taken back automatically.
    /// </summary>
    public static bool CanReopen(PurchaseOrder po) =>
        po.Status is PurchaseOrderStatusFlow.WaitingForPayment or PurchaseOrderStatusFlow.PaymentDone
            or PurchaseOrderStatusFlow.PrRejected or PurchaseOrderStatusFlow.WaitingForPr;

    /// <summary>Cancelled/returned POs keep their status; money on them can still be taken back.</summary>
    public static bool IsClosed(PurchaseOrder po) =>
        po.Status is PurchaseOrderStatusFlow.Cancelled or PurchaseOrderStatusFlow.Returned;
}
