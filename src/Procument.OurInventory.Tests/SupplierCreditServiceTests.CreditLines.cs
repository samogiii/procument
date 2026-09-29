using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Procument.Module.Purchasing.Entities;
using Procument.Module.Purchasing.Services;
using Procument.Module.Sales.DTOs;
using Procument.Module.Sales.Entities;
using Procument.Module.Sales.Services;
using Xunit;

namespace Procument.OurInventory.Tests;

/// <summary>Supplier Credits E10 — credit lines: buy now, pay back through the normal POP flow.</summary>
public sealed partial class SupplierCreditServiceTests
{
    private const string Line = SupplierCreditAccountKinds.CreditLine;

    [SkippableFact]
    public async Task Credit_line_limit_can_be_raised_and_lowered_but_never_below_what_is_owed()
    {
        Fixture.RequireDatabase();
        var (supplierId, company) = await NewSupplierAndCompanyAsync();
        var po = await NewPoAsync(supplierId, company, 20_000m);

        await using var scope = Fixture.Scope();
        var credits = Credits(scope);
        var set = await credits.SetCreditLineAsync(new SetSupplierCreditLineRequest(supplierId, company, 50_000m, null, Guid.NewGuid()), Fixture.UserId);
        Assert.Equal(("In", SupplierCreditSources.CreditLimit, 50_000m), (set.Entry.Direction, set.Entry.Source, set.Balance));
        await credits.SetCreditLineAsync(new SetSupplierCreditLineRequest(supplierId, company, 60_000m, "raised", Guid.NewGuid()), Fixture.UserId);

        var used = await credits.ApplyToPoAsync(po.PoId, new ApplySupplierCreditRequest(po.PrId, 20_000m, Guid.NewGuid(), Line), Fixture.UserId);
        Assert.Equal((40_000m, true), (used.Balance, used.Complete));

        var tooLow = await Assert.ThrowsAsync<SupplierCreditException>(() =>
            credits.SetCreditLineAsync(new SetSupplierCreditLineRequest(supplierId, company, 15_000m, null, Guid.NewGuid()), Fixture.UserId));
        Assert.Contains("owed", tooLow.Message);
        var lowered = await credits.SetCreditLineAsync(new SetSupplierCreditLineRequest(supplierId, company, 25_000m, null, Guid.NewGuid()), Fixture.UserId);
        Assert.Equal(5_000m, lowered.Balance);

        var row = (await credits.GetAccountAsync(lowered.Entry.AccountId))!;
        Assert.Equal((Line, 25_000m, 20_000m, 5_000m), (row.Kind, row.CreditLimit!.Value, row.Owed, row.Balance));
        Assert.NotNull(row.OldestUnpaidAt);
        await Assert.ThrowsAsync<SupplierCreditException>(() =>
            credits.ReverseAsync(set.Entry.Id, new ReverseSupplierCreditRequest("undo limit", Guid.NewGuid()), Fixture.UserId));
        await AssertLedgerConsistentAsync(supplierId, company, 5_000m, Line);
    }

    [SkippableFact]
    public async Task Prepaid_credit_and_credit_line_of_one_company_are_separate_and_both_suggested()
    {
        Fixture.RequireDatabase();
        var (supplierId, company) = await NewSupplierAndCompanyAsync();
        await CreditNoteAsync(supplierId, company, 300m);
        await using (var scope = Fixture.Scope())
            await Credits(scope).SetCreditLineAsync(new SetSupplierCreditLineRequest(supplierId, company, 1_000m, null, Guid.NewGuid()), Fixture.UserId);
        var po = await NewPoAsync(supplierId, company, 1_000m);

        await using (var scope = Fixture.Scope())
        {
            var credits = Credits(scope);
            var suggestion = (await credits.GetSuggestionAsync(po.PoId))!;
            Assert.Equal((300m, 300m), (suggestion.UsableBalance, suggestion.SuggestedAmount));
            Assert.Equal((1_000m, 0m, 1_000m), (suggestion.CreditLine!.Available, suggestion.CreditLine.Owed, suggestion.CreditLine.SuggestedAmount));

            // Default kind = prepaid; the credit line is only used when asked for.
            var prepaid = await credits.ApplyToPoAsync(po.PoId, new ApplySupplierCreditRequest(po.PrId, 300m, Guid.NewGuid()), Fixture.UserId);
            Assert.Equal(0m, prepaid.Balance);
            var line = await credits.ApplyToPoAsync(po.PoId, new ApplySupplierCreditRequest(po.PrId, 700m, Guid.NewGuid(), Line), Fixture.UserId);
            Assert.Equal((300m, true), (line.Balance, line.Complete));
            await Assert.ThrowsAsync<SupplierCreditException>(() =>
                credits.ApplyToPoAsync(po.PoId, new ApplySupplierCreditRequest(po.PrId, 1m, Guid.NewGuid(), "Loan"), Fixture.UserId));

            Assert.Single(await credits.GetBalancesAsync(supplierId, Line));
            Assert.Empty(await credits.GetBalancesAsync(supplierId));   // prepaid is used up
            var owing = await credits.GetAccountsAsync(new SupplierCreditAccountQuery { Kind = Line, OnlyOwing = true, SupplierIds = [supplierId] });
            Assert.Equal((1, 700m), (owing.TotalCount, owing.TotalAmountSum!.Value));
        }
        await AssertLedgerConsistentAsync(supplierId, company, 0m);
        await AssertLedgerConsistentAsync(supplierId, company, 300m, Line);
    }

    [SkippableFact]
    public async Task Paying_back_a_credit_line_uses_the_wallet_pop_and_bank_fee_flow_and_makes_it_usable_again()
    {
        Fixture.RequireDatabase();
        var (supplierId, company) = await NewSupplierAndCompanyAsync();
        var po = await NewPoAsync(supplierId, company, 20_000m);
        var wallet = await NewWalletAsync(deposit: 50_000m);
        long accountId;
        await using (var scope = Fixture.Scope())
        {
            accountId = (await Credits(scope).SetCreditLineAsync(new SetSupplierCreditLineRequest(supplierId, company, 50_000m, null, Guid.NewGuid()), Fixture.UserId)).Entry.AccountId;
            await Credits(scope).ApplyToPoAsync(po.PoId, new ApplySupplierCreditRequest(po.PrId, 20_000m, Guid.NewGuid(), Line), Fixture.UserId);
        }

        SupplierCreditRepaymentRow draft;
        await using (var scope = Fixture.Scope())
        {
            var credits = Credits(scope);
            await Assert.ThrowsAsync<SupplierCreditException>(() =>
                credits.SaveRepaymentDraftAsync(accountId, new SaveSupplierCreditRepaymentRequest(wallet, 20_000.01m, null), Fixture.UserId));
            var first = await credits.SaveRepaymentDraftAsync(accountId, new SaveSupplierCreditRepaymentRequest(wallet, 10_000m, null), Fixture.UserId);
            draft = await credits.SaveRepaymentDraftAsync(accountId, new SaveSupplierCreditRepaymentRequest(wallet, 12_000m, null), Fixture.UserId);
            Assert.Equal((first.Id, "READY FOR POP", 12_000m), (draft.Id, draft.Status, draft.Amount));   // one open repayment, updated

            // The generic PR endpoints must not touch a repayment.
            var prs = scope.ServiceProvider.GetRequiredService<IPaymentRequestService>();
            await Assert.ThrowsAsync<ArgumentException>(() => prs.DeleteAsync(draft.Id));
            await Assert.ThrowsAsync<ArgumentException>(() => prs.UpdateAmountAsync(draft.Id, 1m));
        }

        await using (var scope = Fixture.Scope())
        {
            var repaid = await Credits(scope).UploadRepaymentPopAsync(accountId, draft.Id, Upload(SupplierCreditDocumentCategories.Other, "pop.pdf"), Fixture.UserId);
            Assert.Equal((SupplierCreditSources.Repayment, "In", 42_000m), (repaid.Entry.Source, repaid.Entry.Direction, repaid.Balance));
            Assert.Null(repaid.Entry.PaymentRequestId);   // never counted as paying a PO
            var again = await Credits(scope).UploadRepaymentPopAsync(accountId, draft.Id, Upload(SupplierCreditDocumentCategories.Other, "pop.pdf"), Fixture.UserId);
            Assert.Equal(repaid.Entry.Id, again.Entry.Id);   // a retried upload does not pay twice

            var db = Db(scope);
            var pop = await db.PaymentTransactions.SingleAsync(t => t.PaymentRequestId == draft.Id && t.ToType == "SupplierCredit");
            Assert.Equal((12_000m, "Withdraw", (long?)supplierId), (pop.Amount, pop.Type, pop.ToSupplierId));
            Assert.NotNull(pop.PopUploadId);
            Assert.Equal(20_000m, await SupplierPaymentLedger.PaidByPoAsync(db, po.PoId));   // PO paid totals unchanged
            await Assert.ThrowsAsync<SupplierCreditException>(() =>
                Credits(scope).SaveRepaymentDraftAsync(accountId, new SaveSupplierCreditRepaymentRequest(wallet, 1m, null), Fixture.UserId));
        }

        await using (var scope = Fixture.Scope())
        {
            await Assert.ThrowsAsync<SupplierCreditException>(() =>
                Credits(scope).FinalizeRepaymentAsync(accountId, draft.Id, new FinalizeSupplierCreditRepaymentRequest(11_999m)));
            var done = await Credits(scope).FinalizeRepaymentAsync(accountId, draft.Id, new FinalizeSupplierCreditRepaymentRequest(12_050m));
            Assert.Equal(("PAID", 12_000m, 50m), (done.Status, done.WalletAmount!.Value, done.BankFee!.Value));

            var walletRow = await scope.ServiceProvider.GetRequiredService<IPaymentBoxService>().GetByIdAsync(wallet);
            Assert.Equal(50_000m - 12_050m, walletRow!.Balance);
            Assert.Single((await Credits(scope).GetRepaymentsAsync(accountId))!);
            var summary = await Credits(scope).GetSummaryAsync();
            Assert.True(summary.CreditLinesOwingCount >= 1);
        }

        // Revolving: what was paid back can be used again.
        var po2 = await NewPoAsync(supplierId, company, 30_000m);
        await using (var scope = Fixture.Scope())
        {
            var reused = await Credits(scope).ApplyToPoAsync(po2.PoId, new ApplySupplierCreditRequest(po2.PrId, 30_000m, Guid.NewGuid(), Line), Fixture.UserId);
            Assert.Equal(12_000m, reused.Balance);
        }
        await AssertLedgerConsistentAsync(supplierId, company, 12_000m, Line);
    }

    [SkippableFact]
    public async Task Correcting_the_rate_of_a_repayment_pop_rebalances_its_bank_fee()
    {
        Fixture.RequireDatabase();
        var (supplierId, company) = await NewSupplierAndCompanyAsync();
        var po = await NewPoAsync(supplierId, company, 100m);
        long walletId, accountId;
        await using (var scope = Fixture.Scope())
        {
            var db = Db(scope);
            var box = new PaymentBox { Name = $"Wallet-{Guid.NewGuid():N}", CompanyPresetId = Fixture.PresetId, Currency = "CNY" };
            db.Add(box);
            await db.SaveChangesAsync();
            walletId = box.Id;
            accountId = (await Credits(scope).SetCreditLineAsync(new SetSupplierCreditLineRequest(supplierId, company, 100m, null, Guid.NewGuid()), Fixture.UserId)).Entry.AccountId;
            await Credits(scope).ApplyToPoAsync(po.PoId, new ApplySupplierCreditRequest(po.PrId, 100m, Guid.NewGuid(), Line), Fixture.UserId);
        }

        long prId;
        await using (var scope = Fixture.Scope())
        {
            prId = (await Credits(scope).SaveRepaymentDraftAsync(accountId, new SaveSupplierCreditRepaymentRequest(walletId, 100m, 7m), Fixture.UserId)).Id;
            await Credits(scope).UploadRepaymentPopAsync(accountId, prId, Upload(SupplierCreditDocumentCategories.Other, "pop.png"), Fixture.UserId);
            await Credits(scope).FinalizeRepaymentAsync(accountId, prId, new FinalizeSupplierCreditRepaymentRequest(750m));   // fee 50 CNY
        }

        await using (var scope = Fixture.Scope())
        {
            var db = Db(scope);
            var pop = await db.PaymentTransactions.SingleAsync(t => t.PaymentRequestId == prId && t.ToType == "SupplierCredit");
            var result = await scope.ServiceProvider.GetRequiredService<IPaymentBoxService>()
                .UpdateExchangeRateAsync(walletId, pop.Id, 7.2m, "bank rate", Fixture.UserId);
            Assert.Equal(30m, result!.BankFeeAmount);   // 750 − 100 × 7.2
        }
        await AssertLedgerConsistentAsync(supplierId, company, 100m, Line);
    }
}
