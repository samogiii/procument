using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Procument.Data;
using Procument.Module.Catalog.Entities;
using Procument.Module.Purchasing.Entities;
using Procument.Module.Purchasing.Services;
using Procument.Module.Sales.Controllers;
using Procument.Module.Sales.DTOs;
using Procument.Module.Sales.Entities;
using Procument.Module.Sales.Services;
using Procument.Shared.Services;
using Xunit;

namespace Procument.OurInventory.Tests;

/// <summary>Supplier Credits E3/E4/E9 — plan §5.4 scenarios 1–8 and credit proofs.</summary>
[Collection(StockDatabaseCollection.Name)]
public sealed partial class SupplierCreditServiceTests(StockDatabaseFixture fx)
{
    /// <summary>The fixture for the other partial files (primary-constructor parameters are visible in this part only).</summary>
    private StockDatabaseFixture Fixture => fx;

    // §5.4 (1) — the example from the plan, end to end with the real wallet payment endpoints.
    [SkippableFact]
    public async Task Credit_of_company_B_is_invisible_to_company_A_and_counts_toward_a_company_B_payment()
    {
        fx.RequireDatabase();
        var (supplierId, companyB) = await NewSupplierAndCompanyAsync();
        await CreditNoteAsync(supplierId, companyB, 1200m);

        var poA = await NewPoAsync(supplierId, fx.PresetId, 2000m);
        await using (var scope = fx.Scope())
        {
            var suggestion = (await Credits(scope).GetSuggestionAsync(poA.PoId))!;
            Assert.Equal(0m, suggestion.UsableBalance);
            Assert.Equal(0m, suggestion.SuggestedAmount);
            Assert.Equal(1200m, Assert.Single(suggestion.OtherCompanies).Balance);
            Assert.True(suggestion.CanChangeCompany);
            await Assert.ThrowsAsync<SupplierCreditException>(() =>
                Credits(scope).ApplyToPoAsync(poA.PoId, new ApplySupplierCreditRequest(poA.PrId, 100m, Guid.NewGuid()), fx.UserId));
        }

        var poB = await NewPoAsync(supplierId, companyB, 2000m);
        await using (var scope = fx.Scope())
        {
            var suggestion = (await Credits(scope).GetSuggestionAsync(poB.PoId))!;
            Assert.Equal((1200m, 1200m), (suggestion.UsableBalance, suggestion.SuggestedAmount));
            Assert.Empty(suggestion.OtherCompanies);

            var applied = await Credits(scope).ApplyToPoAsync(poB.PoId, new ApplySupplierCreditRequest(poB.PrId, 800m, Guid.NewGuid()), fx.UserId);
            Assert.Equal((400m, 800m, 1200m, "PARTIALLY PAID", "PartiallyPaid", false),
                (applied.Balance, applied.PaidAmount, applied.RemainingAmount, applied.PrStatus, applied.PoPaymentStatus, applied.Complete));
        }

        var walletId = await NewWalletAsync();
        await using (var scope = fx.Scope())
        {
            var payments = PaymentsController(scope);
            Assert.IsType<BadRequestObjectResult>(await payments.SaveDraft(poB.PoId, new SaveSupplierPaymentDraftRequest(poB.PrId, walletId, 1300m, null, null)));
            Assert.IsType<OkObjectResult>(await payments.SaveDraft(poB.PoId, new SaveSupplierPaymentDraftRequest(poB.PrId, walletId, 1200m, null, null)));
        }
        await SimulatePopUploadAsync(poB.PoId, poB.PrId);
        await using (var scope = fx.Scope())
        {
            Assert.IsType<OkObjectResult>(await PaymentsController(scope).FinalizeAmount(poB.PoId, new FinalizeSupplierPaymentRequest(1200m)));
            var db = Db(scope);
            var po = await db.PurchaseOrders.SingleAsync(p => p.Id == poB.PoId);
            var pr = await db.Set<PaymentRequest>().SingleAsync(p => p.Id == poB.PrId);
            Assert.Equal(("PAID", "Submitted", PurchaseOrderStatusFlow.PaymentDone), (pr.Status, po.PaymentStatus, po.Status));
            Assert.Equal(2000m, await SupplierPaymentLedger.PaidByPoAsync(db, poB.PoId));
        }
        await AssertLedgerConsistentAsync(supplierId, companyB, 400m);
    }

    // §5.4 (2)
    [SkippableFact]
    public async Task Apply_is_capped_by_balance_pr_remaining_and_a_saved_wallet_draft()
    {
        fx.RequireDatabase();
        var (supplierId, company) = await NewSupplierAndCompanyAsync();
        await CreditNoteAsync(supplierId, company, 500m);
        var po = await NewPoAsync(supplierId, company, 300m);

        await using (var scope = fx.Scope())
        {
            var ex = await Assert.ThrowsAsync<SupplierCreditException>(() =>
                Credits(scope).ApplyToPoAsync(po.PoId, new ApplySupplierCreditRequest(po.PrId, 300.01m, Guid.NewGuid()), fx.UserId));
            Assert.Contains("left to pay", ex.Message);
        }

        var walletId = await NewWalletAsync();
        await using (var scope = fx.Scope())
            Assert.IsType<OkObjectResult>(await PaymentsController(scope).SaveDraft(po.PoId, new SaveSupplierPaymentDraftRequest(po.PrId, walletId, 250m, null, null)));

        await using (var scope = fx.Scope())
        {
            await Assert.ThrowsAsync<SupplierCreditException>(() =>
                Credits(scope).ApplyToPoAsync(po.PoId, new ApplySupplierCreditRequest(po.PrId, 60m, Guid.NewGuid()), fx.UserId));
            var applied = await Credits(scope).ApplyToPoAsync(po.PoId, new ApplySupplierCreditRequest(po.PrId, 50m, Guid.NewGuid()), fx.UserId);
            // A wallet payment is mid-flight: the draft keeps owning the PR status.
            Assert.Equal("READY FOR POP", applied.PrStatus);
        }

        var poSmallCredit = await NewPoAsync(supplierId, company, 1000m);
        await using (var scope = fx.Scope())
        {
            var ex = await Assert.ThrowsAsync<SupplierCreditException>(() =>
                Credits(scope).ApplyToPoAsync(poSmallCredit.PoId, new ApplySupplierCreditRequest(poSmallCredit.PrId, 451m, Guid.NewGuid()), fx.UserId));
            Assert.Contains("450.00 USD credit is available", ex.Message);
        }
    }

    // §5.4 (3)
    [SkippableFact]
    public async Task Credit_covering_the_whole_po_completes_payment_without_wallet_rows()
    {
        fx.RequireDatabase();
        var (supplierId, company) = await NewSupplierAndCompanyAsync();
        await CreditNoteAsync(supplierId, company, 750m);
        var po = await NewPoAsync(supplierId, company, 750m);

        await using var scope = fx.Scope();
        var applied = await Credits(scope).ApplyToPoAsync(po.PoId, new ApplySupplierCreditRequest(po.PrId, 750m, Guid.NewGuid()), fx.UserId);
        Assert.True(applied.Complete);
        Assert.Equal(("PAID", "Submitted", 0m), (applied.PrStatus, applied.PoPaymentStatus, applied.Balance));

        var db = Db(scope);
        db.ChangeTracker.Clear();
        Assert.Equal(PurchaseOrderStatusFlow.PaymentDone, (await db.PurchaseOrders.SingleAsync(p => p.Id == po.PoId)).Status);
        Assert.False(await db.PaymentTransactions.AnyAsync(t => t.PaymentRequestId == po.PrId));
    }

    // §5.4 (4)
    [SkippableFact]
    public async Task Repeating_a_request_id_returns_the_first_result_and_writes_once()
    {
        fx.RequireDatabase();
        var (supplierId, company) = await NewSupplierAndCompanyAsync();
        var requestId = Guid.NewGuid();
        var note = new AddSupplierCreditNoteRequest { SupplierId = supplierId, CompanyPresetId = company, Amount = 100m, SourcePoReference = "Opening balance", RequestId = requestId };

        await using var scope = fx.Scope();
        var first = await Credits(scope).AddCreditNoteAsync(note, [], fx.UserId);
        var second = await Credits(scope).AddCreditNoteAsync(note, [], fx.UserId);
        Assert.Equal(first.Entry.Id, second.Entry.Id);
        Assert.Equal(100m, second.Balance);
        Assert.Equal(1, await Db(scope).SupplierCreditEntries.CountAsync(e => e.RequestId == requestId));

        await Assert.ThrowsAsync<SupplierCreditConflictException>(() => Credits(scope).AddAdjustmentAsync(
            new AddSupplierCreditAdjustmentRequest(supplierId, company, "In", 5m, "reuse", requestId), fx.UserId));
    }

    // §5.4 (5)
    [SkippableFact]
    public async Task Concurrent_applies_cannot_overdraw_the_balance()
    {
        fx.RequireDatabase();
        var (supplierId, company) = await NewSupplierAndCompanyAsync();
        await CreditNoteAsync(supplierId, company, 100m);
        var po1 = await NewPoAsync(supplierId, company, 100m);
        var po2 = await NewPoAsync(supplierId, company, 100m);

        async Task<bool> TryApply((long PoId, long PrId) po)
        {
            await using var scope = fx.Scope();
            try
            {
                await Credits(scope).ApplyToPoAsync(po.PoId, new ApplySupplierCreditRequest(po.PrId, 70m, Guid.NewGuid()), fx.UserId);
                return true;
            }
            catch (SupplierCreditException) { return false; }
            catch (SupplierCreditConflictException) { return false; }
        }

        var results = await Task.WhenAll(TryApply(po1), TryApply(po2));
        Assert.Single(results, ok => ok);
        await AssertLedgerConsistentAsync(supplierId, company, 30m);
    }

    // §5.4 (6)
    [SkippableFact]
    public async Task Reversing_an_application_restores_credit_and_reopens_the_payment()
    {
        fx.RequireDatabase();
        var (supplierId, company) = await NewSupplierAndCompanyAsync();
        await CreditNoteAsync(supplierId, company, 500m);
        var po = await NewPoAsync(supplierId, company, 500m);

        long applicationId;
        await using (var scope = fx.Scope())
            applicationId = (await Credits(scope).ApplyToPoAsync(po.PoId, new ApplySupplierCreditRequest(po.PrId, 500m, Guid.NewGuid()), fx.UserId)).Entry.Id;

        await using (var scope = fx.Scope())
        {
            var reversed = await Credits(scope).ReverseAsync(applicationId, new ReverseSupplierCreditRequest("wrong PO", Guid.NewGuid()), fx.UserId);
            Assert.Equal((500m, "In", "Reversal", applicationId), (reversed.Balance, reversed.Entry.Direction, reversed.Entry.Source, reversed.Entry.ReversesEntryId));

            var db = Db(scope);
            db.ChangeTracker.Clear();
            var poRow = await db.PurchaseOrders.SingleAsync(p => p.Id == po.PoId);
            var prRow = await db.Set<PaymentRequest>().SingleAsync(p => p.Id == po.PrId);
            Assert.Equal((SupplierPaymentCompletion.UnpaidPrStatus, "NotStarted", PurchaseOrderStatusFlow.WaitingForPayment, (DateTime?)null),
                (prRow.Status, poRow.PaymentStatus, poRow.Status, poRow.PaymentSubmittedAt));
            Assert.Equal(0m, await SupplierPaymentLedger.PaidByPrAsync(db, po.PrId));

            await Assert.ThrowsAsync<SupplierCreditException>(() =>
                Credits(scope).ReverseAsync(applicationId, new ReverseSupplierCreditRequest("again", Guid.NewGuid()), fx.UserId));
            await Assert.ThrowsAsync<SupplierCreditException>(() =>
                Credits(scope).ReverseAsync(reversed.Entry.Id, new ReverseSupplierCreditRequest("undo undo", Guid.NewGuid()), fx.UserId));
            Assert.True(await scope.ServiceProvider.GetRequiredService<IPaymentLedgerService>().HasSupplierCreditHistoryAsync(po.PrId));
        }

        var shipped = await NewPoAsync(supplierId, company, 100m);
        long shippedApplication;
        await using (var scope = fx.Scope())
            shippedApplication = (await Credits(scope).ApplyToPoAsync(shipped.PoId, new ApplySupplierCreditRequest(shipped.PrId, 100m, Guid.NewGuid()), fx.UserId)).Entry.Id;
        await using (var scope = fx.Scope())
        {
            var db = Db(scope);
            var shippedPo = await db.PurchaseOrders.SingleAsync(p => p.Id == shipped.PoId);
            shippedPo.Status = PurchaseOrderStatusFlow.WaitingForShipment;
            await db.SaveChangesAsync();
            var ex = await Assert.ThrowsAsync<SupplierCreditException>(() =>
                Credits(scope).ReverseAsync(shippedApplication, new ReverseSupplierCreditRequest("too late", Guid.NewGuid()), fx.UserId));
            Assert.Contains("moved past payment", ex.Message);
        }
        await AssertLedgerConsistentAsync(supplierId, company, 400m);
    }

    // §5.4 (7)
    [SkippableFact]
    public async Task Wallet_top_up_moves_money_into_credit_without_paying_any_po_and_reverses_cleanly()
    {
        fx.RequireDatabase();
        var (supplierId, company) = await NewSupplierAndCompanyAsync();
        var walletId = await NewWalletAsync(deposit: 1000m);
        var po = await NewPoAsync(supplierId, company, 300m);

        SupplierCreditWriteResult topUp;
        await using (var scope = fx.Scope())
        {
            topUp = await Credits(scope).AddWalletTopUpAsync(
                new AddSupplierCreditTopUpRequest(supplierId, company, walletId, 250m, null, "deposit for next orders", Guid.NewGuid()), fx.UserId);
            Assert.Equal(250m, topUp.Balance);
            Assert.NotNull(topUp.Entry.WalletTransactionId);

            var wallets = scope.ServiceProvider.GetRequiredService<IPaymentBoxService>();
            Assert.Equal(750m, (await wallets.GetByIdAsync(walletId))!.Balance);
            Assert.Equal(0m, await SupplierPaymentLedger.PaidByPoAsync(Db(scope), po.PoId));
            await Assert.ThrowsAsync<ArgumentException>(() => wallets.DeleteTransactionAsync(topUp.Entry.WalletTransactionId!.Value));
        }

        await using (var scope = fx.Scope())
        {
            var reversed = await Credits(scope).ReverseAsync(topUp.Entry.Id, new ReverseSupplierCreditRequest("supplier refunded", Guid.NewGuid()), fx.UserId);
            Assert.Equal(0m, reversed.Balance);
            Assert.Equal(1000m, (await scope.ServiceProvider.GetRequiredService<IPaymentBoxService>().GetByIdAsync(walletId))!.Balance);
        }
        await AssertLedgerConsistentAsync(supplierId, company, 0m);
    }

    [SkippableFact]
    public async Task Out_adjustment_and_reversing_used_credit_cannot_go_negative()
    {
        fx.RequireDatabase();
        var (supplierId, company) = await NewSupplierAndCompanyAsync();
        var note = await CreditNoteAsync(supplierId, company, 100m);
        var po = await NewPoAsync(supplierId, company, 100m);

        await using var scope = fx.Scope();
        await Credits(scope).ApplyToPoAsync(po.PoId, new ApplySupplierCreditRequest(po.PrId, 60m, Guid.NewGuid()), fx.UserId);
        await Assert.ThrowsAsync<SupplierCreditException>(() => Credits(scope).AddAdjustmentAsync(
            new AddSupplierCreditAdjustmentRequest(supplierId, company, "Out", 41m, "too much", Guid.NewGuid()), fx.UserId));
        var ex = await Assert.ThrowsAsync<SupplierCreditException>(() =>
            Credits(scope).ReverseAsync(note.Entry.Id, new ReverseSupplierCreditRequest("note was wrong", Guid.NewGuid()), fx.UserId));
        Assert.Contains("has been used", ex.Message);
        await AssertLedgerConsistentAsync(supplierId, company, 40m);
    }

    // ── E4: change a PO's buying company (plan rule 8, §5.4 (8)) ───────────────

    [SkippableFact]
    public async Task Switching_a_po_from_company_A_to_B_makes_B_credit_usable_and_moves_its_prs()
    {
        fx.RequireDatabase();
        var (supplierId, companyB) = await NewSupplierAndCompanyAsync();
        await CreditNoteAsync(supplierId, companyB, 1200m);
        var po = await NewPoAsync(supplierId, fx.PresetId, 2000m);
        var walletA = await NewWalletAsync();
        await using (var scope = fx.Scope())
        {
            var db = Db(scope);
            var row = await db.PurchaseOrders.SingleAsync(p => p.Id == po.PoId);
            row.PreferredWalletId = walletA;          // legacy preference pointing at a Company A wallet
            (await db.Set<PaymentRequest>().SingleAsync(p => p.Id == po.PrId)).CompanyPresetId = fx.PresetId;
            await db.SaveChangesAsync();
        }

        await using (var scope = fx.Scope())
            await Companies(scope).ChangeCompanyAsync(po.PoId, companyB, null);

        await using (var scope = fx.Scope())
        {
            var db = Db(scope);
            var row = await db.PurchaseOrders.SingleAsync(p => p.Id == po.PoId);
            Assert.Equal((companyB, (long?)null), (row.CompanyPresetId, row.PreferredWalletId));
            Assert.Equal(companyB, (await db.Set<PaymentRequest>().SingleAsync(p => p.Id == po.PrId)).CompanyPresetId);

            var suggestion = (await Credits(scope).GetSuggestionAsync(po.PoId))!;
            Assert.Equal((1200m, 1200m), (suggestion.UsableBalance, suggestion.SuggestedAmount));
            var applied = await Credits(scope).ApplyToPoAsync(po.PoId, new ApplySupplierCreditRequest(po.PrId, 800m, Guid.NewGuid()), fx.UserId);
            Assert.Equal(400m, applied.Balance);
        }
    }

    [SkippableFact]
    public async Task Company_change_is_blocked_by_payments_credit_and_drafts_and_reopens_after_a_full_reversal()
    {
        fx.RequireDatabase();
        var (supplierId, companyB) = await NewSupplierAndCompanyAsync();
        await CreditNoteAsync(supplierId, companyB, 500m);

        // Wallet payment recorded
        var paidPo = await NewPoAsync(supplierId, fx.PresetId, 100m);
        var wallet = await NewWalletAsync();
        await using (var scope = fx.Scope())
        {
            var db = Db(scope);
            db.Add(new PaymentTransaction
            {
                PaymentBoxId = wallet, PaymentRequestId = paidPo.PrId, Type = "Withdraw", Amount = 10m,
                FromType = "MotherWallet", ToType = "Supplier", ToSupplierId = supplierId, TxCurrency = "USD", IsAuto = true,
            });
            await db.SaveChangesAsync();
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => Companies(scope).ChangeCompanyAsync(paidPo.PoId, companyB, null));
            Assert.Contains("already been recorded", ex.Message);
        }

        // Wallet draft saved (nothing paid yet)
        var draftPo = await NewPoAsync(supplierId, fx.PresetId, 100m);
        await using (var scope = fx.Scope())
            Assert.IsType<OkObjectResult>(await PaymentsController(scope).SaveDraft(draftPo.PoId, new SaveSupplierPaymentDraftRequest(draftPo.PrId, wallet, 50m, null, null)));
        await using (var scope = fx.Scope())
        {
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => Companies(scope).ChangeCompanyAsync(draftPo.PoId, companyB, null));
            Assert.Contains("in progress", ex.Message);
        }

        // Credit applied → blocked; fully reversed → allowed again (net paid is 0)
        var creditPo = await NewPoAsync(supplierId, companyB, 100m);
        long applicationId;
        await using (var scope = fx.Scope())
        {
            applicationId = (await Credits(scope).ApplyToPoAsync(creditPo.PoId, new ApplySupplierCreditRequest(creditPo.PrId, 40m, Guid.NewGuid()), fx.UserId)).Entry.Id;
            Assert.False((await Credits(scope).GetSuggestionAsync(creditPo.PoId))!.CanChangeCompany);
            await Assert.ThrowsAsync<ArgumentException>(() => Companies(scope).ChangeCompanyAsync(creditPo.PoId, fx.PresetId, null));
        }
        await using (var scope = fx.Scope())
            await Credits(scope).ReverseAsync(applicationId, new ReverseSupplierCreditRequest("wrong company", Guid.NewGuid()), fx.UserId);
        await using (var scope = fx.Scope())
        {
            Assert.True((await Credits(scope).GetSuggestionAsync(creditPo.PoId))!.CanChangeCompany);
            await Companies(scope).ChangeCompanyAsync(creditPo.PoId, fx.PresetId, null);
        }

        // Past payment
        var shippedPo = await NewPoAsync(supplierId, fx.PresetId, 100m);
        await using (var scope = fx.Scope())
        {
            var db = Db(scope);
            (await db.PurchaseOrders.SingleAsync(p => p.Id == shippedPo.PoId)).Status = PurchaseOrderStatusFlow.WaitingForShipment;
            await db.SaveChangesAsync();
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => Companies(scope).ChangeCompanyAsync(shippedPo.PoId, companyB, null));
            Assert.Contains("moved past payment", ex.Message);
        }
    }

    [SkippableFact]
    public async Task Company_change_enforces_the_new_company_warehouses_and_active_companies()
    {
        fx.RequireDatabase();
        var (supplierId, companyB) = await NewSupplierAndCompanyAsync();
        var po = await NewPoAsync(supplierId, fx.PresetId, 100m);
        await using var scope = fx.Scope();
        var db = Db(scope);
        (await db.PurchaseOrders.SingleAsync(p => p.Id == po.PoId)).DestinationWarehouseId = fx.MainWarehouseId;
        db.Add(new CompanyPresetWarehouse { CompanyPresetId = companyB, WarehouseId = fx.SecondWarehouseId });
        var inactive = new CompanyPreset { Name = $"Closed {Guid.NewGuid():N}"[..20], IsActive = false };
        db.Add(inactive);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => Companies(scope).ChangeCompanyAsync(po.PoId, companyB, null));
        Assert.Contains("Choose one of its warehouses", ex.Message);
        await Assert.ThrowsAsync<ArgumentException>(() => Companies(scope).ChangeCompanyAsync(po.PoId, companyB, fx.MainWarehouseId));
        await Assert.ThrowsAsync<ArgumentException>(() => Companies(scope).ChangeCompanyAsync(po.PoId, inactive.Id, null));

        await Companies(scope).ChangeCompanyAsync(po.PoId, companyB, fx.SecondWarehouseId);
        db.ChangeTracker.Clear();
        var row = await db.PurchaseOrders.SingleAsync(p => p.Id == po.PoId);
        Assert.Equal((companyB, (long?)fx.SecondWarehouseId), (row.CompanyPresetId, row.DestinationWarehouseId));
    }

    // ── E9: proof of where a credit came from ────────────────────────────────

    [SkippableFact]
    public async Task Credit_note_records_its_origin_po_credit_memo_and_documents()
    {
        fx.RequireDatabase();
        var (supplierId, company) = await NewSupplierAndCompanyAsync();
        var originPo = await NewPoAsync(supplierId, company, 500m);

        SupplierCreditWriteResult written;
        await using (var scope = fx.Scope())
        {
            written = await Credits(scope).AddCreditNoteAsync(new AddSupplierCreditNoteRequest
            {
                SupplierId = supplierId, CompanyPresetId = company, Amount = 120m,
                SourcePurchaseOrderId = originPo.PoId, CreditMemo = "CM-2026-014 returned 2 pcs", RequestId = Guid.NewGuid(),
            },
            [
                Upload(SupplierCreditDocumentCategories.SupplierPI, "supplier-pi.pdf"),
                Upload(SupplierCreditDocumentCategories.Other, "credit-memo.png"),
                Upload(SupplierCreditDocumentCategories.Other, "email.eml"),
            ], fx.UserId);
        }

        var entry = written.Entry;
        Assert.Equal((originPo.PoId, "CM-2026-014 returned 2 pcs", (string?)null), (entry.SourcePoId!.Value, entry.CreditMemo, entry.SourcePoReference));
        Assert.StartsWith("/purchase-orders/", entry.SourcePoLink);
        Assert.Equal(3, entry.Documents.Count);
        Assert.Single(entry.Documents, d => d.Category == SupplierCreditDocumentCategories.SupplierPI && d.OriginalFileName == "supplier-pi.pdf");

        await using (var scope = fx.Scope())
        {
            var file = await Credits(scope).OpenDocumentAsync(entry.Documents[0].Id);
            Assert.NotNull(file);
            await using var stream = file!.Value.Stream;
            Assert.Equal("supplier-pi.pdf", file.Value.FileName);
            Assert.True(stream.Length > 0);

            // More proof later: attaching never changes the balance.
            var added = await Credits(scope).AddDocumentsAsync(entry.Id, [Upload(SupplierCreditDocumentCategories.CreditMemo, "memo-signed.pdf")], fx.UserId);
            Assert.Single(added);
            var ledger = await Credits(scope).GetEntriesAsync(entry.AccountId, 1, 25);
            Assert.Equal(4, ledger!.Items.Single(e => e.Id == entry.Id).Documents.Count);
            Assert.Equal(120m, (await Credits(scope).GetAccountAsync(entry.AccountId))!.Balance);
        }
    }

    [SkippableFact]
    public async Task Credit_note_needs_exactly_one_origin_from_this_supplier_and_allowed_files()
    {
        fx.RequireDatabase();
        var (supplierId, company) = await NewSupplierAndCompanyAsync();
        var (otherSupplierId, _) = await NewSupplierAndCompanyAsync();
        var otherSuppliersPo = await NewPoAsync(otherSupplierId, company, 100m);
        var ownPo = await NewPoAsync(supplierId, company, 100m);

        AddSupplierCreditNoteRequest Note(long? poId, string? text) => new()
        {
            SupplierId = supplierId, CompanyPresetId = company, Amount = 10m,
            SourcePurchaseOrderId = poId, SourcePoReference = text, RequestId = Guid.NewGuid(),
        };

        await using var scope = fx.Scope();
        var service = Credits(scope);
        await Assert.ThrowsAsync<SupplierCreditException>(() => service.AddCreditNoteAsync(Note(null, null), [], fx.UserId));
        await Assert.ThrowsAsync<SupplierCreditException>(() => service.AddCreditNoteAsync(Note(ownPo.PoId, "also text"), [], fx.UserId));
        var wrongSupplier = await Assert.ThrowsAsync<SupplierCreditException>(() => service.AddCreditNoteAsync(Note(otherSuppliersPo.PoId, null), [], fx.UserId));
        Assert.Contains("one of our POs with", wrongSupplier.Message);
        await Assert.ThrowsAsync<SupplierCreditException>(() => service.AddCreditNoteAsync(Note(ownPo.PoId, null),
            [Upload(SupplierCreditDocumentCategories.Other, "run-me.exe")], fx.UserId));
        await Assert.ThrowsAsync<SupplierCreditException>(() => service.AddCreditNoteAsync(Note(ownPo.PoId, null),
            [Upload(SupplierCreditDocumentCategories.SupplierPI, "a.pdf"), Upload(SupplierCreditDocumentCategories.SupplierPI, "b.pdf")], fx.UserId));

        var ok = await service.AddCreditNoteAsync(Note(ownPo.PoId, null), [], fx.UserId);
        Assert.Equal(ownPo.PoId, ok.Entry.SourcePoId);
        Assert.Contains(await service.SearchPurchaseOrdersAsync(supplierId, null), p => p.Id == ownPo.PoId);
        Assert.DoesNotContain(await service.SearchPurchaseOrdersAsync(supplierId, null), p => p.Id == otherSuppliersPo.PoId);
    }

    private static SupplierCreditUpload Upload(string category, string fileName) =>
        new(category, fileName, 4, () => new MemoryStream([1, 2, 3, 4]));

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static IPurchaseOrderCompanyService Companies(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IPurchaseOrderCompanyService>();

    private static ISupplierCreditService Credits(AsyncServiceScope scope) => scope.ServiceProvider.GetRequiredService<ISupplierCreditService>();
    private static AppDbContext Db(AsyncServiceScope scope) => scope.ServiceProvider.GetRequiredService<AppDbContext>();

    private SupplierPaymentsController PaymentsController(AsyncServiceScope scope) =>
        new(scope.ServiceProvider.GetRequiredService<DbContext>(),
            scope.ServiceProvider.GetRequiredService<IDocumentStorageService>(),
            scope.ServiceProvider.GetRequiredService<IPaymentBoxService>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, fx.UserId.ToString())], "test")),
                },
            },
        };

    /// <summary>A fresh supplier and a second company per test, so credit accounts never collide between tests.</summary>
    private async Task<(long SupplierId, long CompanyId)> NewSupplierAndCompanyAsync()
    {
        await using var scope = fx.Scope();
        var db = Db(scope);
        var supplier = new Supplier { Name = $"Credit Supplier {Guid.NewGuid():N}"[..30], Status = "Approved", IsActive = true };
        var company = new CompanyPreset { Name = $"Company {Guid.NewGuid():N}"[..20], IsActive = true };
        db.AddRange(supplier, company);
        await db.SaveChangesAsync();
        return (supplier.Id, company.Id);
    }

    private async Task<SupplierCreditWriteResult> CreditNoteAsync(long supplierId, long companyId, decimal amount)
    {
        await using var scope = fx.Scope();
        return await Credits(scope).AddCreditNoteAsync(new AddSupplierCreditNoteRequest
        {
            SupplierId = supplierId, CompanyPresetId = companyId, Amount = amount, SourcePoReference = "Opening balance",
            CreditMemo = "CN-1", RequestId = Guid.NewGuid(),
        }, [], fx.UserId);
    }

    /// <summary>A PO whose payment request has been accepted, i.e. ready to be paid.</summary>
    private async Task<(long PoId, long PrId)> NewPoAsync(long supplierId, long companyId, decimal total)
    {
        await using var scope = fx.Scope();
        var db = Db(scope);
        var po = new PurchaseOrder
        {
            PONumber = $"PO-{Guid.NewGuid():N}"[..16], SupplierId = supplierId, CompanyPresetId = companyId, TotalAmount = total,
            Status = PurchaseOrderStatusFlow.WaitingForPayment, AdminApproval = "Approved", PaymentApproval = "Accepted",
        };
        db.Add(po);
        await db.SaveChangesAsync();
        var pr = new PaymentRequest { PRId = Random.Shared.Next(100000, 999999), POId = po.Id, Amount = total, Status = SupplierPaymentCompletion.UnpaidPrStatus };
        db.Add(pr);
        await db.SaveChangesAsync();
        return (po.Id, pr.Id);
    }

    private async Task<long> NewWalletAsync(decimal deposit = 0m)
    {
        await using var scope = fx.Scope();
        var db = Db(scope);
        var box = new PaymentBox { Name = $"Wallet-{Guid.NewGuid():N}", CompanyPresetId = fx.PresetId, Currency = "USD" };
        db.Add(box);
        await db.SaveChangesAsync();
        if (deposit > 0)
        {
            db.Add(new PaymentTransaction { PaymentBoxId = box.Id, Type = "Deposit", Amount = deposit, FromType = "MotherWallet", ToType = "MotherWallet" });
            await db.SaveChangesAsync();
        }
        return box.Id;
    }

    /// <summary>What POST supplier-payments/po/{id}/pop does, without writing a file.</summary>
    private async Task SimulatePopUploadAsync(long poId, long prId)
    {
        await using var scope = fx.Scope();
        var db = Db(scope);
        var po = await db.PurchaseOrders.SingleAsync(p => p.Id == poId);
        var pr = await db.Set<PaymentRequest>().SingleAsync(p => p.Id == prId);
        db.Add(new PaymentTransaction
        {
            PaymentBoxId = pr.PendingWalletId!.Value, PaymentRequestId = pr.Id, Amount = pr.PendingPaymentAmount!.Value,
            TxCurrency = "USD", ExchangeRate = pr.PendingExchangeRate, Type = "Withdraw", FromType = "MotherWallet",
            ToType = "Supplier", ToSupplierId = po.SupplierId, IsAuto = true, PopUploadId = pr.PendingUploadId,
        });
        pr.Status = "WAITING FOR FINAL AMOUNT";
        po.PaymentStatus = "WaitingForFinalAmount";
        await db.SaveChangesAsync();
    }

    /// <summary>db-summary-queries.sql K3a/K3b in code: cached balance == ledger, and BalanceAfter is a correct running total.</summary>
    private async Task AssertLedgerConsistentAsync(long supplierId, long companyId, decimal expectedBalance, string kind = SupplierCreditAccountKinds.Prepaid)
    {
        await using var scope = fx.Scope();
        var db = Db(scope);
        var account = await db.SupplierCreditAccounts.SingleAsync(a => a.SupplierId == supplierId && a.CompanyPresetId == companyId && a.Kind == kind);
        var entries = await db.SupplierCreditEntries.Where(e => e.AccountId == account.Id)
            .OrderBy(e => e.CreatedAt).ThenBy(e => e.Id).ToListAsync();
        var running = 0m;
        foreach (var entry in entries)
        {
            running += entry.Direction == "In" ? entry.Amount : -entry.Amount;
            Assert.Equal(running, entry.BalanceAfter);
        }
        Assert.Equal(expectedBalance, account.Balance);
        Assert.Equal(account.Balance, running);
        if (kind == SupplierCreditAccountKinds.CreditLine)   // db-summary-queries.sql K3d
            Assert.Equal(account.CreditLimit, entries.Where(e => e.Source == SupplierCreditSources.CreditLimit)
                .Sum(e => e.Direction == "In" ? e.Amount : -e.Amount));
    }
}
