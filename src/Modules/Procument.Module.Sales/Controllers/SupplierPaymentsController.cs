using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Purchasing.Entities;
using Procument.Module.Purchasing.Services;
using Procument.Module.Sales.Entities;
using Procument.Module.Sales.Services;
using Procument.Shared.Services;

namespace Procument.Module.Sales.Controllers;

public record SaveSupplierPaymentDraftRequest(long PaymentRequestId, long WalletId, decimal Amount, decimal? ExchangeRate, decimal? WireFee);
public record FinalizeSupplierPaymentRequest(decimal FinalWalletAmount);

[ApiController]
[Route("api/supplier-payments")]
[Authorize(Roles = "Payment,AHM,Admin,SuperAdmin")]
public class SupplierPaymentsController(DbContext db, IDocumentStorageService storage, IPaymentBoxService wallets) : ControllerBase
{
    private const string Folder = "Our POP to Supplier";
    private const string WaitingForFinalAmount = "WaitingForFinalAmount";

    [HttpGet("wallets")]
    public async Task<IActionResult> Wallets() => Ok(await wallets.GetSimpleListAsync());

    [HttpGet("po/{poId:long}")]
    public async Task<IActionResult> History(long poId) => Ok(await db.Set<PaymentTransaction>()
        .Where(t => t.PaymentRequest!.POId == poId && t.Type == "Withdraw"
            && (t.ToType == "Supplier" || t.ToType == "BankFee"))
        .OrderByDescending(t => t.CreatedAt)
        .Select(t => new
        {
            t.Id,
            t.PaymentRequestId,
            PrNumber = t.PaymentRequest!.PRId,
            t.Amount,
            WalletName = t.PaymentBox.Name,
            WalletCurrency = t.PaymentBox.Currency,
            WalletAmount = t.Amount * (t.ExchangeRate ?? 1m),
            t.PopFileName,
            t.CreatedAt,
            Category = t.ToType == "BankFee" ? "Bank Fee and Others" : "Supplier Payment",
            t.Notes,
        })
        .ToListAsync());

    [HttpGet("po/{poId:long}/draft")]
    public async Task<IActionResult> Draft(long poId)
    {
        var draft = await (
            from pr in db.Set<PaymentRequest>()
            join wallet in db.Set<PaymentBox>() on pr.PendingWalletId equals wallet.Id
            where pr.POId == poId && pr.PendingUploadId != null && pr.PendingPaymentAmount != null
            orderby pr.Id descending
            select new
            {
                PaymentRequestId = pr.Id,
                PrNumber = pr.PRId,
                WalletId = wallet.Id,
                WalletName = wallet.Name,
                WalletCurrency = wallet.Currency,
                Amount = pr.PendingPaymentAmount!.Value,
                ExchangeRate = pr.PendingExchangeRate,
                WireFee = pr.PO!.ImportDetail != null ? pr.PO.ImportDetail.Wirefee ?? 0 : 0,
                UploadId = pr.PendingUploadId!.Value,
                pr.Status,
            }).FirstOrDefaultAsync();

        if (draft == null) return Ok(null);
        var pop = await db.Set<PaymentTransaction>()
            .Where(t => t.PopUploadId == draft.UploadId && t.ToType == "Supplier")
            .Select(t => new { t.Id, t.PopFileName, t.CreatedAt, InitialWalletAmount = t.Amount * (t.ExchangeRate ?? 1m) })
            .FirstOrDefaultAsync();
        return Ok(new { draft.PaymentRequestId, draft.PrNumber, draft.WalletId, draft.WalletName,
            draft.WalletCurrency, draft.Amount, draft.ExchangeRate, draft.WireFee,
            draft.UploadId, draft.Status, Pop = pop });
    }

    [HttpPost("po/{poId:long}/draft")]
    public async Task<IActionResult> SaveDraft(long poId, [FromBody] SaveSupplierPaymentDraftRequest request)
    {
        if (request.Amount <= 0 || decimal.Round(request.Amount, 2) != request.Amount)
            return BadRequest(new { message = "Enter a positive USD amount with at most two decimal places." });
        if (request.WireFee is < 0 || (request.WireFee.HasValue && decimal.Round(request.WireFee.Value, 2) != request.WireFee.Value))
            return BadRequest(new { message = "Enter a non-negative wire fee with at most two decimal places." });

        return await db.Database.CreateExecutionStrategy().ExecuteAsync<IActionResult>(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var po = await db.Set<PurchaseOrder>().Include(p => p.ImportDetail).FirstOrDefaultAsync(p => p.Id == poId);
            if (po == null) return NotFound();
            if (po.PaymentApproval != "Accepted" || po.Status is "Cancelled" or "Returned")
                return BadRequest(new { message = "The payment request must be accepted before payment." });
            if (po.PaymentStatus == WaitingForFinalAmount)
                return BadRequest(new { message = "Confirm the final bank amount for the current POP before starting another payment." });

            var pr = await db.Set<PaymentRequest>().FirstOrDefaultAsync(p => p.Id == request.PaymentRequestId && p.POId == poId);
            if (pr == null) return BadRequest(new { message = "Select a payment request belonging to this PO." });
            var wallet = await db.Set<PaymentBox>().FirstOrDefaultAsync(w => w.Id == request.WalletId);
            if (wallet == null) return BadRequest(new { message = "Select the wallet used for this payment." });

            var rate = string.Equals(wallet.Currency, "USD", StringComparison.OrdinalIgnoreCase) ? 1m : request.ExchangeRate;
            if (rate is null or <= 0 || decimal.Round(rate.Value, 6) != rate.Value)
                return BadRequest(new { message = "Enter the wallet currency amount per 1 USD (up to six decimal places)." });

            var oldWireFee = po.ImportDetail?.Wirefee ?? 0;
            var newWireFee = request.WireFee ?? oldWireFee;
            var wireFeeDelta = newWireFee - oldWireFee;
            if (wireFeeDelta != 0)
            {
                if (po.ImportDetail == null)
                {
                    po.ImportDetail = new POImportDetail { PurchaseOrderId = po.Id, Wirefee = newWireFee };
                    db.Set<POImportDetail>().Add(po.ImportDetail);
                }
                else
                {
                    po.ImportDetail.Wirefee = newWireFee;
                }
                pr.Amount = (pr.Amount ?? SupplierPaymentAmounts.Total(po) - wireFeeDelta) + wireFeeDelta;
            }

            var poTotal = SupplierPaymentAmounts.Total(po);
            var payments = db.Set<PaymentTransaction>().Where(t => t.PaymentRequest!.POId == poId
                && t.Type == "Withdraw" && t.ToType == "Supplier");
            var poPaid = await payments.SumAsync(t => t.Amount);
            var prPaid = await payments.Where(t => t.PaymentRequestId == request.PaymentRequestId).SumAsync(t => t.Amount);
            var otherAllocated = await db.Set<PaymentRequest>()
                .Where(p => p.POId == poId && p.Id != pr.Id)
                .SumAsync(p => p.Amount ?? 0);
            if ((pr.Amount ?? poTotal) < prPaid || (pr.Amount ?? poTotal) > poTotal - otherAllocated)
                return BadRequest(new { message = "The wire fee would make the payment request exceed the PO balance or fall below its paid amount." });
            if (request.Amount > (pr.Amount ?? poTotal) - prPaid || request.Amount > poTotal - poPaid)
                return BadRequest(new { message = "The payment exceeds the remaining PR or PO balance. Refresh and check the amount." });

            pr.PendingWalletId = wallet.Id;
            pr.PendingPaymentAmount = request.Amount;
            pr.PendingExchangeRate = rate;
            pr.PendingUploadId ??= Guid.NewGuid();
            pr.Status = "READY FOR POP";
            await db.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { paymentRequestId = pr.Id, prNumber = pr.PRId, walletId = wallet.Id,
                walletName = wallet.Name, walletCurrency = wallet.Currency, amount = request.Amount,
                exchangeRate = rate, wireFee = newWireFee, poTotalAmount = poTotal,
                paymentRequestAmount = pr.Amount, uploadId = pr.PendingUploadId, status = pr.Status });
        });
    }

    [HttpGet("{id:long}/file")]
    public async Task<IActionResult> File(long id)
    {
        var tx = await db.Set<PaymentTransaction>()
            .Include(t => t.ToSupplier)
            .Include(t => t.PaymentRequest).ThenInclude(pr => pr!.PO)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (tx?.PopFileName == null || tx.PopInvoiceNumber == null || tx.ToSupplier == null) return NotFound();
        var file = storage.OpenFileInSupplierCategory(tx.PopInvoiceNumber, tx.ToSupplier.Name, Folder, tx.PopFileName);
        if (file == null) return NotFound();
        if (tx.PaymentRequest?.PO is { } po && po.Status == PurchaseOrderStatusFlow.PaymentDone)
        {
            await PurchaseOrderStatusFlow.ApplyAsync(db, po, PurchaseOrderStatusFlow.WaitingForShipment);
            await db.SaveChangesAsync();
        }
        new FileExtensionContentTypeProvider().TryGetContentType(tx.PopFileName, out var mime);
        return base.File(file.Value.Stream, mime ?? "application/octet-stream", tx.PopFileName);
    }

    [HttpPost("po/{poId:long}/pop")]
    [RequestSizeLimit(104_857_600)]
    public async Task<IActionResult> Upload(long poId, [FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Choose a POP image or PDF first." });

        return await db.Database.CreateExecutionStrategy().ExecuteAsync<IActionResult>(async () =>
        {
            db.ChangeTracker.Clear();
            // Do not hold broad Serializable locks while an IIS upload is processed.
            // The POP upload id provides idempotency, while ReadCommitted avoids production
            // requests waiting behind unrelated payment reads/writes.
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            var po = await db.Set<PurchaseOrder>().Include(p => p.Supplier).Include(p => p.ImportDetail).FirstOrDefaultAsync(p => p.Id == poId);
            if (po == null) return NotFound();
            var pr = await db.Set<PaymentRequest>()
                .Where(p => p.POId == poId && p.PendingUploadId != null && p.PendingPaymentAmount != null && p.PendingWalletId != null)
                .OrderByDescending(p => p.Id).FirstOrDefaultAsync();
            if (pr == null)
                return BadRequest(new { message = "Save the payment request, amount, and wallet before uploading the POP." });

            var uploadId = pr.PendingUploadId!.Value;
            var walletId = pr.PendingWalletId!.Value;
            var amount = pr.PendingPaymentAmount!.Value;
            var existing = await db.Set<PaymentTransaction>().FirstOrDefaultAsync(t => t.PopUploadId == uploadId);
            if (existing != null) return Ok(new { existing.Id, fileName = existing.PopFileName, waitingForFinalAmount = true });

            var wallet = await db.Set<PaymentBox>().FirstOrDefaultAsync(w => w.Id == walletId);
            if (wallet == null) return BadRequest(new { message = "The saved wallet no longer exists. Save the payment details again." });
            var rate = string.Equals(wallet.Currency, "USD", StringComparison.OrdinalIgnoreCase) ? 1m : pr.PendingExchangeRate;
            if (rate is null or <= 0) return BadRequest(new { message = "The saved exchange rate is invalid." });

            var invoiceNumber = await db.Set<Invoice>().Where(i => i.Id == po.InvoiceId)
                .Select(i => i.InvoiceNumber).FirstOrDefaultAsync() ?? po.PONumber;
            using var stream = file.OpenReadStream();
            var savedName = storage.SaveFileInSupplierCategory(invoiceNumber, po.Supplier.Name, Folder,
                $"PR-{pr.PRId}-{uploadId:N}-{Path.GetFileName(file.FileName)}", stream);

            var payment = new PaymentTransaction
            {
                PaymentBoxId = wallet.Id, PaymentRequestId = pr.Id, InvoiceId = po.InvoiceId,
                Amount = amount, TxCurrency = "USD", ExchangeRate = rate,
                Type = "Withdraw", FromType = "MotherWallet", ToType = "Supplier", ToSupplierId = po.SupplierId,
                IsAuto = true, PopUploadId = uploadId, PopFileName = savedName, PopInvoiceNumber = invoiceNumber,
                Notes = $"POP for PR-{pr.PRId} / {po.PONumber}",
            };
            db.Set<PaymentTransaction>().Add(payment);
            pr.Status = "WAITING FOR FINAL AMOUNT";
            po.PaymentStatus = WaitingForFinalAmount;
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(new { payment.Id, fileName = savedName, paidAmount = amount,
                initialWalletAmount = amount * rate.Value, walletCurrency = wallet.Currency, waitingForFinalAmount = true });
        });
    }

    [HttpPost("po/{poId:long}/final-amount")]
    public async Task<IActionResult> FinalizeAmount(long poId, [FromBody] FinalizeSupplierPaymentRequest request)
    {
        if (request.FinalWalletAmount <= 0 || decimal.Round(request.FinalWalletAmount, 2) != request.FinalWalletAmount)
            return BadRequest(new { message = "Enter the bank's final withdrawn amount with at most two decimal places." });

        return await db.Database.CreateExecutionStrategy().ExecuteAsync<IActionResult>(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var po = await db.Set<PurchaseOrder>().Include(p => p.ImportDetail).FirstOrDefaultAsync(p => p.Id == poId);
            if (po == null) return NotFound();
            var pr = await db.Set<PaymentRequest>()
                .Where(p => p.POId == poId && p.PendingUploadId != null && p.Status == "WAITING FOR FINAL AMOUNT")
                .OrderByDescending(p => p.Id).FirstOrDefaultAsync();
            if (pr == null) return BadRequest(new { message = "There is no POP waiting for a final bank amount." });

            var payment = await db.Set<PaymentTransaction>()
                .FirstOrDefaultAsync(t => t.PopUploadId == pr.PendingUploadId && t.ToType == "Supplier");
            if (payment == null) return BadRequest(new { message = "The supplier payment for this POP could not be found." });
            var wallet = await db.Set<PaymentBox>().FirstOrDefaultAsync(w => w.Id == payment.PaymentBoxId);
            if (wallet == null) return BadRequest(new { message = "The payment wallet no longer exists." });

            var initialWalletAmount = Math.Round(payment.Amount * (payment.ExchangeRate ?? 1m), 2, MidpointRounding.AwayFromZero);
            if (request.FinalWalletAmount < initialWalletAmount)
                return BadRequest(new { message = $"The final amount cannot be less than the original wallet debit ({initialWalletAmount:0.00} {wallet.Currency})." });
            var bankFee = request.FinalWalletAmount - initialWalletAmount;
            if (bankFee > 0)
            {
                db.Set<PaymentTransaction>().Add(new PaymentTransaction
                {
                    PaymentBoxId = wallet.Id, PaymentRequestId = pr.Id, InvoiceId = po.InvoiceId,
                    Amount = bankFee, TxCurrency = wallet.Currency,
                    Type = "Withdraw", FromType = "MotherWallet", ToType = "BankFee", IsAuto = true,
                    Notes = $"Bank Fee and Others for PR-{pr.PRId} / {po.PONumber}", CreatedAt = DateTime.UtcNow,
                });
            }

            var poTotal = SupplierPaymentAmounts.Total(po);
            var supplierPayments = db.Set<PaymentTransaction>().Where(t => t.PaymentRequest!.POId == poId
                && t.Type == "Withdraw" && t.ToType == "Supplier");
            var poPaid = await supplierPayments.SumAsync(t => t.Amount);
            var prPaid = await supplierPayments.Where(t => t.PaymentRequestId == pr.Id).SumAsync(t => t.Amount);
            pr.Status = prPaid >= (pr.Amount ?? poTotal) ? "PAID" : "PARTIALLY PAID";
            pr.PendingWalletId = null;
            pr.PendingPaymentAmount = null;
            pr.PendingExchangeRate = null;
            pr.PendingUploadId = null;

            var complete = poPaid >= poTotal;
            po.PaymentStatus = complete ? "Submitted" : "PartiallyPaid";
            if (complete)
            {
                po.PaymentSubmittedAt = DateTime.UtcNow;
                po.PaymentSubmittedBy = long.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var userId) ? userId : null;
                await PurchaseOrderStatusFlow.ApplyAsync(db, po, PurchaseOrderStatusFlow.PaymentDone);
            }

            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(new { initialWalletAmount, finalWalletAmount = request.FinalWalletAmount, bankFee,
                walletCurrency = wallet.Currency, paidAmount = poPaid, remainingAmount = Math.Max(0, poTotal - poPaid),
                complete, status = po.PaymentStatus });
        });
    }
}
