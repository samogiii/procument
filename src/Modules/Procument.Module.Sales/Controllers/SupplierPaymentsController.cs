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

/// <summary>Accept = saved from the Payment Request tab: saving the details is what accepts the request.</summary>
public record SaveSupplierPaymentDraftRequest(long PaymentRequestId, long WalletId, decimal Amount, decimal? ExchangeRate, decimal? WireFee, bool Accept = false);
/// <summary>
/// The bank's final amount for a POP. With HandlingFee / BankWireFee (wallet currency) the final amount
/// is worked out here: wallet debit (amount × rate) + handling + wire fee; FinalWalletAmount is then ignored.
/// </summary>
public record FinalizeSupplierPaymentRequest(decimal? FinalWalletAmount, decimal? HandlingFee = null, decimal? BankWireFee = null);
public record RejectWarehousePaymentRequest(string? Note);
/// <summary>
/// One row of a PO's supplier payment history. Id is the wallet transaction (null for credit rows);
/// RowKey is unique across both kinds and is what the UI should key on.
/// </summary>
public record SupplierPaymentHistoryRow(
    string RowKey, long? Id, long? CreditEntryId, long? PaymentRequestId, long? PrNumber,
    decimal Amount, string WalletName, string WalletCurrency, decimal WalletAmount,
    string? PopFileName, DateTime CreatedAt, string Category, string? Notes);

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
    public async Task<IActionResult> History(long poId)
    {
        var wallet = await db.Set<PaymentTransaction>()
            .Where(t => t.PaymentRequest!.POId == poId && t.Type == "Withdraw"
                && (t.ToType == "Supplier" || t.ToType == "BankFee"))
            .Select(t => new SupplierPaymentHistoryRow(
                $"tx-{t.Id}",
                t.Id,
                null,
                t.PaymentRequestId,
                t.PaymentRequest!.PRId,
                t.Amount,
                t.PaymentBox.Name,
                t.PaymentBox.Currency,
                t.Amount * (t.ExchangeRate ?? 1m),
                t.PopFileName,
                t.CreatedAt,
                t.ToType == "BankFee" ? "Bank Fee and Others" : "Supplier Payment",
                t.Notes))
            .ToListAsync();

        // Credit applications pay the PO without a wallet; reversals are shown as negative rows.
        var credit = await SupplierPaymentLedger.CreditApplications(db)
            .Where(e => e.PaymentRequest!.POId == poId)
            .Select(e => new SupplierPaymentHistoryRow(
                $"credit-{e.Id}",
                null,
                e.Id,
                e.PaymentRequestId,
                e.PaymentRequest!.PRId,
                e.Direction == SupplierCreditDirections.Out ? e.Amount : -e.Amount,
                "Credit · " + e.Account.CompanyPreset.Name,
                e.Account.Currency,
                e.Direction == SupplierCreditDirections.Out ? e.Amount : -e.Amount,
                null,
                e.CreatedAt,
                e.Source == SupplierCreditSources.Reversal ? "Supplier Credit Reversal" : "Supplier Credit",
                e.Notes))
            .ToListAsync();

        return Ok(wallet.Concat(credit).OrderByDescending(r => r.CreatedAt).ToList());
    }

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
                pr.WarehousePopFileName,
                pr.WarehouseHandlingFee,
                pr.WarehouseBankWireFee,
                pr.WarehouseFinalAmount,
                pr.WarehouseExchangeRate,
                pr.WarehousePaidBy,
                pr.WarehousePaidAt,
                pr.WarehouseRejectNote,
            }).FirstOrDefaultAsync();

        if (draft == null) return Ok(null);
        var pop = await db.Set<PaymentTransaction>()
            .Where(t => t.PopUploadId == draft.UploadId && t.ToType == "Supplier")
            .Select(t => new { t.Id, t.PopFileName, t.CreatedAt, InitialWalletAmount = t.Amount * (t.ExchangeRate ?? 1m) })
            .FirstOrDefaultAsync();
        // Paid in the warehouse app and waiting for review here (see WarehouseAccept / WarehouseReject).
        var warehouse = draft.WarehousePaidAt == null ? null : new
        {
            FileName = draft.WarehousePopFileName,
            HandlingFee = draft.WarehouseHandlingFee ?? 0,
            BankWireFee = draft.WarehouseBankWireFee ?? 0,
            FinalAmount = draft.WarehouseFinalAmount,
            ExchangeRate = draft.WarehouseExchangeRate,
            PaidBy = draft.WarehousePaidBy,
            PaidAt = draft.WarehousePaidAt,
            InitialWalletAmount = Math.Round(draft.Amount * (draft.WarehouseExchangeRate ?? 1m), 2, MidpointRounding.AwayFromZero),
        };
        return Ok(new { draft.PaymentRequestId, draft.PrNumber, draft.WalletId, draft.WalletName,
            draft.WalletCurrency, draft.Amount, draft.ExchangeRate, draft.WireFee,
            draft.UploadId, draft.Status, Pop = pop, Warehouse = warehouse, draft.WarehouseRejectNote });
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
            if (request.Accept && po.PaymentApproval != "Accepted" && po.Status is not ("Cancelled" or "Returned"))
            {
                // Saved from the Payment Request tab: the details are the acceptance. Nothing is
                // stored unless the rest of the checks pass (SaveChanges is only reached at the end).
                po.PaymentApproval = "Accepted";
                po.PaymentApprovalNote = null;
                po.PaymentApprovalAt = DateTime.UtcNow;
                po.PaymentApprovalBy = CurrentUserId();
                await PurchaseOrderStatusFlow.ApplyAsync(db, po, PurchaseOrderStatusFlow.WaitingForPayment);
            }
            if (po.PaymentApproval != "Accepted" || po.Status is "Cancelled" or "Returned")
                return BadRequest(new { message = "The payment request must be accepted before payment." });
            if (po.PaymentStatus == WaitingForFinalAmount)
                return BadRequest(new { message = "Confirm the final bank amount for the current POP before starting another payment." });
            if (await db.Set<PaymentRequest>().AnyAsync(p => p.POId == poId && p.WarehousePaidAt != null))
                return BadRequest(new { message = "The warehouse already paid this request. Accept or reject that payment first." });

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
            // Wallet payments + supplier credit applied, so credit is never paid a second time.
            var poPaid = await SupplierPaymentLedger.PaidByPoAsync(db, poId);
            var prPaid = await SupplierPaymentLedger.PaidByPrAsync(db, request.PaymentRequestId);
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
            var pr = await PendingDraftAsync(poId);
            if (pr == null)
                return BadRequest(new { message = "Save the payment request, amount, and wallet before uploading the POP." });
            if (pr.WarehousePaidAt != null)
                return BadRequest(new { message = "The warehouse already paid this request. Accept or reject that payment instead." });

            var uploadId = pr.PendingUploadId!.Value;
            var existing = await db.Set<PaymentTransaction>().FirstOrDefaultAsync(t => t.PopUploadId == uploadId);
            if (existing != null) return Ok(new { existing.Id, fileName = existing.PopFileName, waitingForFinalAmount = true });

            var invoiceNumber = await PopFolderAsync(po);
            using var stream = file.OpenReadStream();
            var savedName = storage.SaveFileInSupplierCategory(invoiceNumber, po.Supplier.Name, Folder,
                $"PR-{pr.PRId}-{uploadId:N}-{Path.GetFileName(file.FileName)}", stream);

            var (payment, wallet, error) = await AddPopPaymentAsync(po, pr, savedName, invoiceNumber, pr.PendingExchangeRate);
            if (payment == null)
            {
                storage.DeleteFileInSupplierCategory(invoiceNumber, po.Supplier.Name, Folder, savedName);
                return BadRequest(new { message = error });
            }
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(new { payment.Id, fileName = savedName, paidAmount = payment.Amount,
                initialWalletAmount = payment.Amount * payment.ExchangeRate!.Value, walletCurrency = wallet!.Currency, waitingForFinalAmount = true });
        });
    }

    [HttpPost("po/{poId:long}/final-amount")]
    public async Task<IActionResult> FinalizeAmount(long poId, [FromBody] FinalizeSupplierPaymentRequest request)
    {
        var byFees = request.HandlingFee != null || request.BankWireFee != null;
        if (byFees && (request.HandlingFee is < 0 || request.BankWireFee is < 0
                || decimal.Round(request.HandlingFee ?? 0, 2) != (request.HandlingFee ?? 0)
                || decimal.Round(request.BankWireFee ?? 0, 2) != (request.BankWireFee ?? 0)))
            return BadRequest(new { message = "Enter the handling and wire fees as 0 or more with at most two decimal places." });
        if (!byFees && (request.FinalWalletAmount is not > 0 || decimal.Round(request.FinalWalletAmount.Value, 2) != request.FinalWalletAmount))
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

            var (result, error) = byFees
                ? await FinalizeCoreAsync(po, pr, null, request.HandlingFee ?? 0, request.BankWireFee ?? 0)
                : await FinalizeCoreAsync(po, pr, request.FinalWalletAmount, null, null);
            if (result == null) return BadRequest(new { message = error });
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(result);
        });
    }

    // ── Paid in the warehouse app (synced by WarehousePaymentSyncController) ──

    /// <summary>The POP the warehouse uploaded, for review before it is accepted.</summary>
    [HttpGet("po/{poId:long}/warehouse-pop")]
    public async Task<IActionResult> WarehousePop(long poId)
    {
        var po = await db.Set<PurchaseOrder>().AsNoTracking().Include(p => p.Supplier).FirstOrDefaultAsync(p => p.Id == poId);
        var pr = await db.Set<PaymentRequest>().AsNoTracking()
            .Where(p => p.POId == poId && p.WarehousePaidAt != null && p.WarehousePopFileName != null)
            .OrderByDescending(p => p.Id).FirstOrDefaultAsync();
        if (po == null || pr == null) return NotFound();
        var file = storage.OpenFileInSupplierCategory(await PopFolderAsync(po), po.Supplier.Name, Folder, pr.WarehousePopFileName!);
        if (file == null) return NotFound();
        new FileExtensionContentTypeProvider().TryGetContentType(pr.WarehousePopFileName!, out var mime);
        return base.File(file.Value.Stream, mime ?? "application/octet-stream", pr.WarehousePopFileName);
    }

    /// <summary>
    /// Accept what the warehouse paid: records its POP with the warehouse's exchange rate, then its
    /// final bank amount (anything above amount × rate is Bank Fee and Others) — the same rows as
    /// uploading the POP and saving the final amount here. Wallet rows follow the auto-import switch.
    /// </summary>
    [HttpPost("po/{poId:long}/warehouse-accept")]
    public async Task<IActionResult> WarehouseAccept(long poId)
    {
        return await db.Database.CreateExecutionStrategy().ExecuteAsync<IActionResult>(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var po = await db.Set<PurchaseOrder>().Include(p => p.Supplier).Include(p => p.ImportDetail).FirstOrDefaultAsync(p => p.Id == poId);
            if (po == null) return NotFound();
            var pr = await PendingDraftAsync(poId);
            if (pr?.WarehousePaidAt == null || pr.WarehousePopFileName == null || pr.WarehouseFinalAmount == null)
                return BadRequest(new { message = "There is no warehouse payment waiting for review." });
            if (await db.Set<PaymentTransaction>().AnyAsync(t => t.PopUploadId == pr.PendingUploadId))
                return BadRequest(new { message = "A POP was already recorded for this payment." });

            var (payment, _, popError) = await AddPopPaymentAsync(po, pr, pr.WarehousePopFileName,
                await PopFolderAsync(po), pr.WarehouseExchangeRate ?? pr.PendingExchangeRate);
            if (payment == null) return BadRequest(new { message = popError });
            payment.Notes = $"POP for PR-{pr.PRId} / {po.PONumber} (warehouse · {pr.WarehousePaidBy})";
            // The final amount reads the POP row back through the ledger.
            await db.SaveChangesAsync();

            // Fees as entered on the phone; the total is amount × rate + fees again here.
            var (result, error) = await FinalizeCoreAsync(po, pr, null, pr.WarehouseHandlingFee ?? 0, pr.WarehouseBankWireFee ?? 0);
            if (result == null) return BadRequest(new { message = error });
            ClearWarehouse(pr, null);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(result);
        });
    }

    /// <summary>Send the warehouse payment back: its POP is removed and the next sync reopens it on the phone.</summary>
    [HttpPost("po/{poId:long}/warehouse-reject")]
    public async Task<IActionResult> WarehouseReject(long poId, [FromBody] RejectWarehousePaymentRequest request)
    {
        var note = request.Note?.Trim();
        if (string.IsNullOrEmpty(note)) return BadRequest(new { message = "Say what the warehouse should fix." });
        if (note.Length > 500) note = note[..500];

        var po = await db.Set<PurchaseOrder>().Include(p => p.Supplier).FirstOrDefaultAsync(p => p.Id == poId);
        if (po == null) return NotFound();
        var pr = await db.Set<PaymentRequest>()
            .Where(p => p.POId == poId && p.WarehousePaidAt != null)
            .OrderByDescending(p => p.Id).FirstOrDefaultAsync();
        if (pr == null) return BadRequest(new { message = "There is no warehouse payment waiting for review." });

        if (pr.WarehousePopFileName != null)
            storage.DeleteFileInSupplierCategory(await PopFolderAsync(po), po.Supplier.Name, Folder, pr.WarehousePopFileName);
        ClearWarehouse(pr, note);
        await db.SaveChangesAsync();
        return Ok(new { rejected = true });
    }

    // ── Shared by the office and warehouse paths ──

    private Task<PaymentRequest?> PendingDraftAsync(long poId) => db.Set<PaymentRequest>()
        .Where(p => p.POId == poId && p.PendingUploadId != null && p.PendingPaymentAmount != null && p.PendingWalletId != null)
        .OrderByDescending(p => p.Id).FirstOrDefaultAsync();

    /// <summary>POPs are filed under the Sales Order (Stock POs: the PO number).</summary>
    private async Task<string> PopFolderAsync(PurchaseOrder po)
        => await db.Set<Invoice>().Where(i => i.Id == po.InvoiceId).Select(i => i.InvoiceNumber).FirstOrDefaultAsync() ?? po.PONumber;

    /// <summary>The supplier payment row for the saved draft: its USD amount debited at <paramref name="rate"/>. Caller saves.</summary>
    private async Task<(PaymentTransaction? Payment, PaymentBox? Wallet, string? Error)> AddPopPaymentAsync(
        PurchaseOrder po, PaymentRequest pr, string savedName, string invoiceNumber, decimal? rate)
    {
        var wallet = await db.Set<PaymentBox>().FirstOrDefaultAsync(w => w.Id == pr.PendingWalletId);
        if (wallet == null) return (null, null, "The saved wallet no longer exists. Save the payment details again.");
        if (string.Equals(wallet.Currency, "USD", StringComparison.OrdinalIgnoreCase)) rate = 1m;
        if (rate is null or <= 0) return (null, wallet, "The saved exchange rate is invalid.");

        var payment = new PaymentTransaction
        {
            PaymentBoxId = wallet.Id, PaymentRequestId = pr.Id, InvoiceId = po.InvoiceId,
            Amount = pr.PendingPaymentAmount!.Value, TxCurrency = "USD", ExchangeRate = rate,
            Type = "Withdraw", FromType = "MotherWallet", ToType = "Supplier", ToSupplierId = po.SupplierId,
            IsAuto = true, PopUploadId = pr.PendingUploadId, PopFileName = savedName, PopInvoiceNumber = invoiceNumber,
            // Off: still the PO's payment record, just not counted in the wallet.
            InWallet = await WalletImport.IsOnAsync(db),
            // Every PI customer the PO's lines belong to.
            Base = await TransactionBases.ForPaymentRequestAsync(db, pr.Id),
            Notes = $"POP for PR-{pr.PRId} / {po.PONumber}",
        };
        db.Set<PaymentTransaction>().Add(payment);
        pr.PendingExchangeRate = rate;
        pr.Status = "WAITING FOR FINAL AMOUNT";
        po.PaymentStatus = WaitingForFinalAmount;
        return (payment, wallet, null);
    }

    /// <summary>
    /// Bank's final amount for the POP of this draft: fee row, draft cleared, PO progress. Caller saves.
    /// With fees given, the final amount = wallet debit (amount × rate) + handling + wire fee.
    /// </summary>
    private async Task<(object? Result, string? Error)> FinalizeCoreAsync(PurchaseOrder po, PaymentRequest pr,
        decimal? finalWalletAmount, decimal? handlingFee, decimal? bankWireFee)
    {
        var payment = await db.Set<PaymentTransaction>()
            .FirstOrDefaultAsync(t => t.PopUploadId == pr.PendingUploadId && t.ToType == "Supplier");
        if (payment == null) return (null, "The supplier payment for this POP could not be found.");
        var wallet = await db.Set<PaymentBox>().FirstOrDefaultAsync(w => w.Id == payment.PaymentBoxId);
        if (wallet == null) return (null, "The payment wallet no longer exists.");

        var initialWalletAmount = Math.Round(payment.Amount * (payment.ExchangeRate ?? 1m), 2, MidpointRounding.AwayFromZero);
        var byFees = handlingFee != null || bankWireFee != null;
        var final = byFees ? initialWalletAmount + (handlingFee ?? 0) + (bankWireFee ?? 0) : finalWalletAmount ?? 0;
        if (final < initialWalletAmount)
            return (null, $"The final amount cannot be less than the original wallet debit ({initialWalletAmount:0.00} {wallet.Currency}).");
        var bankFee = final - initialWalletAmount;
        if (bankFee > 0)
        {
            var breakdown = byFees ? $" (handling {handlingFee ?? 0:0.00} + wire fee {bankWireFee ?? 0:0.00} {wallet.Currency})" : "";
            db.Set<PaymentTransaction>().Add(new PaymentTransaction
            {
                PaymentBoxId = wallet.Id, PaymentRequestId = pr.Id, InvoiceId = po.InvoiceId,
                Amount = bankFee, TxCurrency = wallet.Currency,
                Type = "Withdraw", FromType = "MotherWallet", ToType = "BankFee", IsAuto = true,
                InWallet = payment.InWallet,   // the fee lives wherever its POP lives
                Base = payment.Base,
                Notes = $"Bank Fee and Others for PR-{pr.PRId} / {po.PONumber}{breakdown}", CreatedAt = DateTime.UtcNow,
            });
        }

        pr.PendingWalletId = null;
        pr.PendingPaymentAmount = null;
        pr.PendingExchangeRate = null;
        pr.PendingUploadId = null;
        var progress = await SupplierPaymentCompletion.ApplyAsync(db, po, pr, CurrentUserId());

        return (new { initialWalletAmount, finalWalletAmount = final, handlingFee, bankWireFee, bankFee,
            walletCurrency = wallet.Currency, paidAmount = progress.PoPaid, remainingAmount = progress.PoRemaining,
            complete = progress.Complete, status = po.PaymentStatus }, null);
    }

    private static void ClearWarehouse(PaymentRequest pr, string? rejectNote)
    {
        pr.WarehousePopFileName = null;
        pr.WarehouseFinalAmount = null;
        pr.WarehouseExchangeRate = null;
        pr.WarehousePaidBy = null;
        pr.WarehousePaidAt = null;
        pr.WarehouseRejectNote = rejectNote;
    }

    private long? CurrentUserId()
        => long.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;
}
