using System.Data;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Catalog.Entities;
using Procument.Module.Purchasing.Entities;
using Procument.Module.Purchasing.Services;
using Procument.Module.Sales.DTOs;
using Procument.Module.Sales.Entities;
using Procument.Shared.DTOs;
using Procument.Shared.Services;

namespace Procument.Module.Sales.Services;

public interface ISupplierCreditService
{
    Task<PagedResult<SupplierCreditAccountRow>> GetAccountsAsync(SupplierCreditAccountQuery query);
    Task<SupplierCreditFilterOptions> GetFilterOptionsAsync(SupplierCreditAccountQuery query);
    Task<SupplierCreditAccountRow?> GetAccountAsync(long accountId);
    Task<PagedResult<SupplierCreditEntryRow>?> GetEntriesAsync(long accountId, int page, int pageSize);
    /// <summary>Usable credit per company with one supplier (balance &gt; 0) for one kind (default Prepaid).</summary>
    Task<List<SupplierCreditCompanyBalance>> GetBalancesAsync(long supplierId, string? kind = null);
    /// <summary>Totals for the tab badges: prepaid credit, and what we owe on credit lines.</summary>
    Task<SupplierCreditSummary> GetSummaryAsync();
    Task<SupplierCreditSuggestion?> GetSuggestionAsync(long poId);
    /// <summary>Approved catalog suppliers matching the text (max 20) — for roles that cannot use /suppliers.</summary>
    Task<List<SupplierCreditOption>> SearchSuppliersAsync(string? q);
    /// <summary>Our active companies (never "(No Company)") — for roles that cannot use /companypresets.</summary>
    Task<List<SupplierCreditOption>> GetCompaniesAsync();

    /// <summary>The supplier's POs (newest first, max 30), offered as the origin of a credit note.</summary>
    Task<List<SupplierCreditPoOption>> SearchPurchaseOrdersAsync(long supplierId, string? q);

    Task<SupplierCreditWriteResult> AddCreditNoteAsync(AddSupplierCreditNoteRequest request, IReadOnlyList<SupplierCreditUpload> uploads, long? userId);
    Task<SupplierCreditWriteResult> AddWalletTopUpAsync(AddSupplierCreditTopUpRequest request, long? userId);
    Task<SupplierCreditWriteResult> AddAdjustmentAsync(AddSupplierCreditAdjustmentRequest request, long? userId);
    Task<ApplySupplierCreditResult> ApplyToPoAsync(long poId, ApplySupplierCreditRequest request, long? userId);
    Task<SupplierCreditWriteResult> ReverseAsync(long entryId, ReverseSupplierCreditRequest request, long? userId);
    /// <summary>Attach more proof to an existing entry. Evidence only — never changes a balance.</summary>
    Task<List<SupplierCreditDocumentRow>> AddDocumentsAsync(long entryId, IReadOnlyList<SupplierCreditUpload> uploads, long? userId);
    Task<(Stream Stream, string FileName)?> OpenDocumentAsync(long documentId);

    // ── Credit lines (E10): buy now, pay back later ──
    /// <summary>Creates the credit line or changes its limit (never below what is owed).</summary>
    Task<SupplierCreditWriteResult> SetCreditLineAsync(SetSupplierCreditLineRequest request, long? userId);
    Task<List<SupplierCreditRepaymentRow>?> GetRepaymentsAsync(long accountId);
    /// <summary>Step 1: amount, wallet and rate saved on a payment request without a PO.</summary>
    Task<SupplierCreditRepaymentRow> SaveRepaymentDraftAsync(long accountId, SaveSupplierCreditRepaymentRequest request, long? userId);
    /// <summary>Step 2: the POP — wallet withdrawal + Repayment entry (the owed amount goes down now).</summary>
    Task<SupplierCreditWriteResult> UploadRepaymentPopAsync(long accountId, long paymentRequestId, SupplierCreditUpload pop, long? userId);
    /// <summary>Step 3: the bank's final debit; the difference is recorded as the bank fee.</summary>
    Task<SupplierCreditRepaymentRow> FinalizeRepaymentAsync(long accountId, long paymentRequestId, FinalizeSupplierCreditRepaymentRequest request);
}

/// <summary>A business-rule violation; the message is shown to the user as-is (HTTP 400).</summary>
public sealed class SupplierCreditException(string message) : Exception(message);

/// <summary>The balance changed underneath the request, or a request id was reused (HTTP 409).</summary>
public sealed class SupplierCreditConflictException(string message) : Exception(message);

/// <summary>
/// Credit we hold with a supplier, per buying company. Every write is one Serializable transaction
/// that locks the account row, is idempotent on RequestId, and keeps Balance == Σ ledger.
/// "Paid" totals always come from <see cref="SupplierPaymentLedger"/>.
/// </summary>
public sealed class SupplierCreditService(
    DbContext db,
    IDocumentStorageService storage,
    IPurchaseOrderCompanyService poCompany) : ISupplierCreditService
{
    public const string Currency = "USD";
    /// <summary>Credit-note files live under Documents/…/Supplier Credits/{Supplier}/Credit Notes.</summary>
    public const string DocumentRoot = "Supplier Credits";
    public const string DocumentFolder = "Credit Notes";
    public const int MaxFilesPerRequest = 10;
    /// <summary>Same folder the PO payment POPs use, so /supplier-payments/{txId}/file serves repayment POPs too.</summary>
    public const string PopFolder = "Our POP to Supplier";
    private const string ReadyForPop = "READY FOR POP";
    private const string WaitingForFinalAmount = "WAITING FOR FINAL AMOUNT";
    private const string Paid = "PAID";
    private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".pdf", ".png", ".jpg", ".jpeg", ".webp", ".doc", ".docx", ".xls", ".xlsx", ".eml", ".msg" };

    // ── Reads ────────────────────────────────────────────────────────────────

    public async Task<PagedResult<SupplierCreditAccountRow>> GetAccountsAsync(SupplierCreditAccountQuery query)
    {
        var filtered = ApplyFilters(db.Set<SupplierCreditAccount>().AsNoTracking(), query);
        var count = await filtered.CountAsync();
        // Prepaid tab: credit the suppliers hold for us. Credit-line tab: what we owe them.
        var sum = IsCreditLine(query.Kind)
            ? await filtered.SumAsync(a => (a.CreditLimit ?? 0) - a.Balance)
            : await filtered.SumAsync(a => a.Balance);
        var items = await AccountRows(Sort(filtered, query).ApplyPaging(query)).ToListAsync();
        return new PagedResult<SupplierCreditAccountRow>
        {
            Items = items, TotalCount = count, Page = query.Page, PageSize = query.PageSize, TotalAmountSum = sum,
        };
    }

    public async Task<SupplierCreditFilterOptions> GetFilterOptionsAsync(SupplierCreditAccountQuery query)
    {
        var root = db.Set<SupplierCreditAccount>().AsNoTracking();
        var supplierIds = await ApplyFilters(root, query, "supplier").Select(a => a.SupplierId).Distinct().ToListAsync();
        var companyIds = await ApplyFilters(root, query, "company").Select(a => a.CompanyPresetId).Distinct().ToListAsync();
        return new SupplierCreditFilterOptions
        {
            Suppliers = await db.Set<Supplier>().Where(s => supplierIds.Contains(s.Id)).OrderBy(s => s.Name)
                .Select(s => new SupplierCreditOption(s.Id, s.Name)).ToListAsync(),
            Companies = await db.Set<CompanyPreset>().Where(p => companyIds.Contains(p.Id)).OrderBy(p => p.Name)
                .Select(p => new SupplierCreditOption(p.Id, p.Name)).ToListAsync(),
        };
    }

    public Task<SupplierCreditAccountRow?> GetAccountAsync(long accountId) =>
        AccountRows(db.Set<SupplierCreditAccount>().AsNoTracking().Where(a => a.Id == accountId)).FirstOrDefaultAsync();

    public async Task<PagedResult<SupplierCreditEntryRow>?> GetEntriesAsync(long accountId, int page, int pageSize)
    {
        if (!await db.Set<SupplierCreditAccount>().AnyAsync(a => a.Id == accountId)) return null;
        var entries = db.Set<SupplierCreditEntry>().AsNoTracking().Where(e => e.AccountId == accountId);
        var count = await entries.CountAsync();
        var paging = new PageQuery { Page = page, PageSize = pageSize };
        var items = await WithDocumentsAsync(
            await EntryRows(entries.OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id).ApplyPaging(paging)).ToListAsync());
        return new PagedResult<SupplierCreditEntryRow>
        {
            Items = items, TotalCount = count, Page = paging.Page, PageSize = paging.PageSize,
        };
    }

    public Task<List<SupplierCreditCompanyBalance>> GetBalancesAsync(long supplierId, string? kind = null)
    {
        var wanted = RequireKind(kind);
        return db.Set<SupplierCreditAccount>().AsNoTracking()
            .Where(a => a.SupplierId == supplierId && a.Currency == Currency && a.Kind == wanted && a.Balance > 0)
            .OrderBy(a => a.CompanyPreset.Name)
            .Select(a => new SupplierCreditCompanyBalance(a.CompanyPresetId, a.CompanyPreset.Name, a.Balance, a.Kind))
            .ToListAsync();
    }

    public async Task<SupplierCreditSummary> GetSummaryAsync()
    {
        var accounts = db.Set<SupplierCreditAccount>().AsNoTracking();
        var prepaid = await accounts.Where(a => a.Kind == SupplierCreditAccountKinds.Prepaid).SumAsync(a => a.Balance);
        var owing = accounts.Where(a => a.Kind == SupplierCreditAccountKinds.CreditLine && (a.CreditLimit ?? 0) - a.Balance > 0);
        return new SupplierCreditSummary(prepaid, await owing.CountAsync(), await owing.SumAsync(a => (a.CreditLimit ?? 0) - a.Balance));
    }

    public async Task<SupplierCreditSuggestion?> GetSuggestionAsync(long poId)
    {
        var po = await db.Set<PurchaseOrder>().AsNoTracking()
            .Include(p => p.Supplier).Include(p => p.ImportDetail)
            .FirstOrDefaultAsync(p => p.Id == poId);
        if (po == null) return null;

        var companyId = await EffectiveCompanyAsync(po);
        var accounts = await db.Set<SupplierCreditAccount>().AsNoTracking()
            .Where(a => a.SupplierId == po.SupplierId && a.Currency == Currency)
            .Select(a => new { a.Id, a.CompanyPresetId, CompanyName = a.CompanyPreset.Name, a.Balance, a.Kind, a.CreditLimit })
            .ToListAsync();
        var own = accounts.FirstOrDefault(a => a.CompanyPresetId == companyId && a.Kind == SupplierCreditAccountKinds.Prepaid);
        var ownLine = accounts.FirstOrDefault(a => a.CompanyPresetId == companyId && a.Kind == SupplierCreditAccountKinds.CreditLine);

        var poTotal = SupplierPaymentAmounts.Total(po);
        var poPaid = await SupplierPaymentLedger.PaidByPoAsync(db, po.Id);
        var pending = await PendingDraftsAsync(po.Id);
        var poRemaining = Math.Max(0, poTotal - poPaid - pending.Values.Sum());
        var prs = await db.Set<PaymentRequest>().AsNoTracking().Where(p => p.POId == po.Id).OrderBy(p => p.Id).ToListAsync();
        var paidByPr = await SupplierPaymentLedger.PaidByPrAsync(db, prs.Select(p => p.Id));
        var prOptions = prs
            .Select(p => new SupplierCreditPrOption(p.Id, p.PRId,
                Math.Max(0, (p.Amount ?? poTotal) - paidByPr.GetValueOrDefault(p.Id) - pending.GetValueOrDefault(p.Id))))
            .Where(p => p.Remaining > 0)
            .ToList();

        var cannotApply = CannotApplyReason(po);
        var walletInProgress = await WalletInProgressAsync(po.Id);
        // Same rule as PATCH /purchase-orders/{id}/company-preset (plan rule 8).
        var changeBlocked = await poCompany.GetChangeBlockedReasonAsync(po);

        var usable = own?.Balance ?? 0;
        decimal Suggest(decimal available) => cannotApply == null && available > 0 && prOptions.Count > 0
            ? Math.Min(available, Math.Min(prOptions[0].Remaining, poRemaining))
            : 0;
        return new SupplierCreditSuggestion
        {
            PoId = po.Id,
            SupplierId = po.SupplierId,
            SupplierName = po.Supplier.Name,
            CompanyPresetId = companyId,
            CompanyPresetName = own?.CompanyName ?? ownLine?.CompanyName ?? (companyId.HasValue
                ? await db.Set<CompanyPreset>().Where(p => p.Id == companyId.Value).Select(p => p.Name).FirstOrDefaultAsync()
                : null),
            AccountId = own?.Id,
            UsableBalance = usable,
            PoRemaining = poRemaining,
            PrOptions = prOptions,
            SuggestedAmount = Suggest(usable),
            CreditLine = ownLine == null ? null : new SupplierCreditLineInfo(
                ownLine.Id, ownLine.CreditLimit ?? 0, ownLine.Balance, (ownLine.CreditLimit ?? 0) - ownLine.Balance, Suggest(ownLine.Balance)),
            OtherCompanies = accounts
                .Where(a => a.CompanyPresetId != companyId && a.Balance > 0)
                .OrderBy(a => a.Kind).ThenByDescending(a => a.Balance)
                .Select(a => new SupplierCreditCompanyBalance(a.CompanyPresetId, a.CompanyName, a.Balance, a.Kind))
                .ToList(),
            CanApply = cannotApply == null,
            CannotApplyReason = cannotApply,
            WalletPaymentInProgress = walletInProgress,
            CanChangeCompany = changeBlocked == null,
            ChangeCompanyBlockedReason = changeBlocked,
        };
    }

    public Task<List<SupplierCreditOption>> SearchSuppliersAsync(string? q)
    {
        var query = db.Set<Supplier>().AsNoTracking().Where(s => s.Status == "Approved");
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(s => s.Name.Contains(term));
        }
        return query.OrderBy(s => s.Name).Take(20).Select(s => new SupplierCreditOption(s.Id, s.Name)).ToListAsync();
    }

    public Task<List<SupplierCreditOption>> GetCompaniesAsync() =>
        db.Set<CompanyPreset>().AsNoTracking()
            .Where(p => p.IsActive && p.Name != PaymentBoxService.NoCompanyPresetName)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Name)
            .Select(p => new SupplierCreditOption(p.Id, p.Name))
            .ToListAsync();

    public async Task<(Stream Stream, string FileName)?> OpenDocumentAsync(long documentId)
    {
        var doc = await db.Set<SupplierCreditDocument>().AsNoTracking()
            .Where(d => d.Id == documentId)
            .Select(d => new { d.FileName, d.OriginalFileName, SupplierName = d.Entry.Account.Supplier.Name })
            .FirstOrDefaultAsync();
        if (doc == null) return null;
        var file = storage.OpenFileInSupplierCategory(DocumentRoot, doc.SupplierName, DocumentFolder, doc.FileName);
        return file == null ? null : (file.Value.Stream, doc.OriginalFileName);
    }

    public Task<List<SupplierCreditPoOption>> SearchPurchaseOrdersAsync(long supplierId, string? q)
    {
        var query = db.Set<PurchaseOrder>().AsNoTracking().Where(p => p.SupplierId == supplierId);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(p => p.PONumber.Contains(term) || (p.SupplierPIRef != null && p.SupplierPIRef.Contains(term)));
        }
        return query.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id).Take(30)
            .Select(p => new SupplierCreditPoOption(
                p.Id,
                p.PONumber,
                db.Set<CompanyPreset>().Where(c => c.Id == p.CompanyPresetId).Select(c => c.Name).FirstOrDefault(),
                p.Status,
                p.TotalAmount,
                p.CreatedAt))
            .ToListAsync();
    }

    // ── Writes ───────────────────────────────────────────────────────────────

    public Task<SupplierCreditWriteResult> AddCreditNoteAsync(AddSupplierCreditNoteRequest request, IReadOnlyList<SupplierCreditUpload> uploads, long? userId)
    {
        ValidateAmount(request.Amount);
        var sourceText = Clean(request.SourcePoReference, 100, "origin text");
        if (request.SourcePurchaseOrderId.HasValue == (sourceText != null))
            throw new SupplierCreditException("Say where the credit comes from: pick one of our POs or type a reference — one of the two.");
        var creditMemo = Clean(request.CreditMemo, 200, "credit memo");
        var notes = Clean(request.Notes, 500, "note");
        ValidateUploads(uploads);

        return WriteAsync(request.RequestId,
            existing => Replay(existing, SupplierCreditSources.CreditNote),
            async onFailure =>
            {
                var (supplier, preset) = await RequirePartiesAsync(request.SupplierId, request.CompanyPresetId);
                if (request.SourcePurchaseOrderId is { } sourcePoId
                    && !await db.Set<PurchaseOrder>().AnyAsync(p => p.Id == sourcePoId && p.SupplierId == supplier.Id))
                    throw new SupplierCreditException($"The origin PO must be one of our POs with {supplier.Name}.");

                var account = await GetOrCreatePrepaidAccountAsync(supplier.Id, preset.Id);
                var entry = Post(account, SupplierCreditDirections.In, SupplierCreditSources.CreditNote, request.Amount,
                    request.RequestId, userId, e =>
                    {
                        e.SourcePurchaseOrderId = request.SourcePurchaseOrderId;
                        e.SourcePoReference = sourceText;
                        e.CreditMemo = creditMemo;
                        e.Notes = notes;
                    });
                await db.SaveChangesAsync();
                SaveUploads(entry.Id, supplier.Name, uploads, request.RequestId.ToString("N"), userId, onFailure);
                await db.SaveChangesAsync();
                return await WriteResultAsync(entry.Id);
            });
    }

    public async Task<List<SupplierCreditDocumentRow>> AddDocumentsAsync(long entryId, IReadOnlyList<SupplierCreditUpload> uploads, long? userId)
    {
        if (uploads.Count == 0) throw new SupplierCreditException("Choose at least one file.");
        ValidateUploads(uploads);
        var entry = await db.Set<SupplierCreditEntry>().AsNoTracking()
            .Where(e => e.Id == entryId)
            .Select(e => new { e.Id, SupplierName = e.Account.Supplier.Name })
            .FirstOrDefaultAsync() ?? throw new KeyNotFoundException("Credit entry not found.");

        var onFailure = new List<Action>();
        try
        {
            var saved = SaveUploads(entry.Id, entry.SupplierName, uploads, Guid.NewGuid().ToString("N"), userId, onFailure);
            await db.SaveChangesAsync();
            var ids = saved.Select(d => d.Id).ToList();
            return await DocumentRows(db.Set<SupplierCreditDocument>().AsNoTracking().Where(d => ids.Contains(d.Id))).ToListAsync();
        }
        catch
        {
            foreach (var undo in onFailure) { try { undo(); } catch { /* best effort */ } }
            throw;
        }
    }

    private static void ValidateUploads(IReadOnlyList<SupplierCreditUpload> uploads)
    {
        if (uploads.Count > MaxFilesPerRequest)
            throw new SupplierCreditException($"Attach at most {MaxFilesPerRequest} files at a time.");
        if (uploads.Count(u => u.Category == SupplierCreditDocumentCategories.SupplierPI) > 1)
            throw new SupplierCreditException("Attach one supplier PI per request.");
        foreach (var upload in uploads)
        {
            if (!SupplierCreditDocumentCategories.All.Contains(upload.Category))
                throw new SupplierCreditException("Unknown document type.");
            if (!DocumentExtensions.Contains(Path.GetExtension(upload.FileName)))
                throw new SupplierCreditException($"{upload.FileName}: attach PDF, image (PNG, JPG, WEBP), Word, Excel or e-mail (EML, MSG) files.");
            if (Path.GetFileName(upload.FileName).Length > 200)
                throw new SupplierCreditException($"{upload.FileName}: the file name is too long.");
        }
    }

    /// <summary>
    /// Writes the files next to the supplier's other credit documents and adds their rows (not saved yet).
    /// Each saved file registers its own delete in <paramref name="onFailure"/>.
    /// </summary>
    private List<SupplierCreditDocument> SaveUploads(long entryId, string supplierName, IReadOnlyList<SupplierCreditUpload> uploads,
        string prefix, long? userId, List<Action> onFailure)
    {
        var rows = new List<SupplierCreditDocument>();
        foreach (var upload in uploads)
        {
            var original = Path.GetFileName(upload.FileName);
            string savedName;
            using (var content = upload.OpenRead())
                savedName = storage.SaveFileInSupplierCategory(DocumentRoot, supplierName, DocumentFolder, $"{prefix}-{original}", content);
            onFailure.Add(() => storage.DeleteFileInSupplierCategory(DocumentRoot, supplierName, DocumentFolder, savedName));

            var row = new SupplierCreditDocument
            {
                EntryId = entryId, Category = upload.Category, FileName = savedName, OriginalFileName = original,
                SizeBytes = upload.SizeBytes, UploadedByUserId = userId, UploadedAt = DateTime.UtcNow,
            };
            db.Add(row);
            rows.Add(row);
        }
        return rows;
    }

    public Task<SupplierCreditWriteResult> AddWalletTopUpAsync(AddSupplierCreditTopUpRequest request, long? userId)
    {
        ValidateAmount(request.Amount);
        var notes = Clean(request.Notes, 500, "note");

        return WriteAsync(request.RequestId,
            existing => Replay(existing, SupplierCreditSources.WalletTopUp),
            async _ =>
            {
                var (supplier, preset) = await RequirePartiesAsync(request.SupplierId, request.CompanyPresetId);
                var wallet = await db.Set<PaymentBox>().FirstOrDefaultAsync(w => w.Id == request.WalletId)
                    ?? throw new SupplierCreditException("Select the wallet the prepayment is paid from.");
                var rate = WalletRate(wallet, request.ExchangeRate);

                var account = await GetOrCreatePrepaidAccountAsync(supplier.Id, preset.Id);
                var walletTx = new PaymentTransaction
                {
                    PaymentBoxId = wallet.Id, Type = "Withdraw", Amount = request.Amount,
                    TxCurrency = Currency, ExchangeRate = rate,
                    FromType = "MotherWallet", ToType = "SupplierCredit", ToSupplierId = supplier.Id, IsAuto = true,
                    InWallet = await WalletImport.IsOnAsync(db),
                    Notes = Truncate($"Prepayment credit — {supplier.Name} / {preset.Name}" + (notes != null ? $": {notes}" : ""), 1000),
                };
                db.Add(walletTx);
                await db.SaveChangesAsync();

                var entry = Post(account, SupplierCreditDirections.In, SupplierCreditSources.WalletTopUp, request.Amount,
                    request.RequestId, userId, e =>
                    {
                        e.PaymentTransactionId = walletTx.Id;
                        e.Notes = notes;
                    });
                await db.SaveChangesAsync();
                return await WriteResultAsync(entry.Id);
            });
    }

    public Task<SupplierCreditWriteResult> AddAdjustmentAsync(AddSupplierCreditAdjustmentRequest request, long? userId)
    {
        ValidateAmount(request.Amount);
        var direction = request.Direction switch
        {
            SupplierCreditDirections.In => SupplierCreditDirections.In,
            SupplierCreditDirections.Out => SupplierCreditDirections.Out,
            _ => throw new SupplierCreditException("Choose whether the adjustment adds (In) or removes (Out) credit."),
        };
        var notes = Clean(request.Notes, 500, "note") ?? throw new SupplierCreditException("Explain the adjustment in the note.");

        return WriteAsync(request.RequestId,
            existing => Replay(existing, SupplierCreditSources.Adjustment),
            async _ =>
            {
                var (supplier, preset) = await RequirePartiesAsync(request.SupplierId, request.CompanyPresetId);
                var account = direction == SupplierCreditDirections.In
                    ? await GetOrCreatePrepaidAccountAsync(supplier.Id, preset.Id)
                    : await LockAccountAsync(supplier.Id, preset.Id, SupplierCreditAccountKinds.Prepaid)
                        ?? throw new SupplierCreditException($"{preset.Name} has no credit with {supplier.Name}.");

                var entry = Post(account, direction, SupplierCreditSources.Adjustment, request.Amount,
                    request.RequestId, userId, e => e.Notes = notes);
                await db.SaveChangesAsync();
                return await WriteResultAsync(entry.Id);
            });
    }

    public Task<ApplySupplierCreditResult> ApplyToPoAsync(long poId, ApplySupplierCreditRequest request, long? userId)
    {
        ValidateAmount(request.Amount);
        var kind = RequireKind(request.Kind);
        var isLine = IsCreditLine(kind);

        return WriteAsync(request.RequestId,
            async existing =>
            {
                if (existing.Source != SupplierCreditSources.POApplication || existing.PurchaseOrderId != poId
                    || existing.PaymentRequestId != request.PaymentRequestId)
                    throw new SupplierCreditConflictException("This request id was already used for a different credit operation.");
                var po = await db.Set<PurchaseOrder>().Include(p => p.ImportDetail).FirstAsync(p => p.Id == poId);
                var pr = await db.Set<PaymentRequest>().FirstAsync(p => p.Id == request.PaymentRequestId);
                return await ApplyResultAsync(existing.Id, po, pr, await SupplierPaymentCompletion.ProgressAsync(db, po, pr));
            },
            async _ =>
            {
                var po = await db.Set<PurchaseOrder>().Include(p => p.ImportDetail).FirstOrDefaultAsync(p => p.Id == poId)
                    ?? throw new KeyNotFoundException("Purchase order not found.");
                if (CannotApplyReason(po) is { } reason) throw new SupplierCreditException(reason);
                var pr = await db.Set<PaymentRequest>().FirstOrDefaultAsync(p => p.Id == request.PaymentRequestId && p.POId == poId)
                    ?? throw new SupplierCreditException("Select a payment request belonging to this PO.");
                var companyId = await EffectiveCompanyAsync(po)
                    ?? throw new SupplierCreditException("This PO has no buying company, so no supplier credit can be used.");

                var account = await LockAccountAsync(po.SupplierId, companyId, kind);
                if (account == null || account.Balance <= 0)
                    throw new SupplierCreditException(isLine
                        ? "The PO's company has no credit line available with this supplier."
                        : "The PO's company has no credit with this supplier.");

                var poTotal = SupplierPaymentAmounts.Total(po);
                var poPaid = await SupplierPaymentLedger.PaidByPoAsync(db, po.Id);
                var prPaid = await SupplierPaymentLedger.PaidByPrAsync(db, pr.Id);
                var pending = await PendingDraftsAsync(po.Id);
                var prRemaining = Math.Max(0, (pr.Amount ?? poTotal) - prPaid - pending.GetValueOrDefault(pr.Id));
                var poRemaining = Math.Max(0, poTotal - poPaid - pending.Values.Sum());
                if (request.Amount > account.Balance)
                    throw new SupplierCreditException(isLine
                        ? $"Only {account.Balance:0.00} {Currency} is available on the credit line."
                        : $"Only {account.Balance:0.00} {Currency} credit is available.");
                if (request.Amount > prRemaining)
                    throw new SupplierCreditException($"The amount exceeds what is left to pay on PR-{pr.PRId} ({prRemaining:0.00} {Currency}, after any saved wallet payment).");
                if (request.Amount > poRemaining)
                    throw new SupplierCreditException($"The amount exceeds what is left to pay on {po.PONumber} ({poRemaining:0.00} {Currency}, after any saved wallet payment).");

                var entry = Post(account, SupplierCreditDirections.Out, SupplierCreditSources.POApplication, request.Amount,
                    request.RequestId, userId, e =>
                    {
                        e.PurchaseOrderId = po.Id;
                        e.PaymentRequestId = pr.Id;
                        e.Notes = isLine
                            ? $"Credit line used on PR-{pr.PRId} / {po.PONumber} — to be paid back"
                            : $"Credit applied to PR-{pr.PRId} / {po.PONumber}";
                    });
                await db.SaveChangesAsync();

                // A wallet payment mid-flight owns the PR/PO statuses (READY FOR POP / WAITING FOR FINAL AMOUNT);
                // its final-amount step re-evaluates with this credit included.
                var progress = await WalletInProgressAsync(po.Id)
                    ? await SupplierPaymentCompletion.ProgressAsync(db, po, pr)
                    : await SupplierPaymentCompletion.ApplyAsync(db, po, pr, userId);
                await db.SaveChangesAsync();
                return await ApplyResultAsync(entry.Id, po, pr, progress);
            });
    }

    public Task<SupplierCreditWriteResult> ReverseAsync(long entryId, ReverseSupplierCreditRequest request, long? userId)
    {
        var notes = Clean(request.Notes, 500, "note") ?? throw new SupplierCreditException("Explain why the entry is reversed.");

        return WriteAsync(request.RequestId,
            existing => existing.Source == SupplierCreditSources.Reversal && existing.ReversesEntryId == entryId
                ? WriteResultAsync(existing.Id)
                : throw new SupplierCreditConflictException("This request id was already used for a different credit operation."),
            async _ =>
            {
                var original = await db.Set<SupplierCreditEntry>().AsNoTracking().FirstOrDefaultAsync(e => e.Id == entryId)
                    ?? throw new KeyNotFoundException("Credit entry not found.");
                if (original.Source == SupplierCreditSources.Reversal)
                    throw new SupplierCreditException("A reversal cannot be reversed.");
                if (original.Source == SupplierCreditSources.CreditLimit)
                    throw new SupplierCreditException("Change the credit limit instead of reversing a limit entry.");
                if (await db.Set<SupplierCreditEntry>().AnyAsync(e => e.ReversesEntryId == entryId))
                    throw new SupplierCreditException("This entry has already been reversed.");

                var account = await LockAccountAsync(original.AccountId)
                    ?? throw new KeyNotFoundException("Credit account not found.");
                var direction = original.Direction == SupplierCreditDirections.In ? SupplierCreditDirections.Out : SupplierCreditDirections.In;
                if (direction == SupplierCreditDirections.Out && account.Balance < original.Amount)
                    throw new SupplierCreditException($"Only {account.Balance:0.00} {Currency} of this credit is left — the rest has been used, so the entry cannot be reversed.");

                PurchaseOrder? po = null;
                PaymentRequest? pr = null;
                if (original.Source == SupplierCreditSources.POApplication)
                {
                    po = await db.Set<PurchaseOrder>().Include(p => p.ImportDetail).FirstAsync(p => p.Id == original.PurchaseOrderId);
                    pr = await db.Set<PaymentRequest>().FirstAsync(p => p.Id == original.PaymentRequestId);
                    if (!IsClosed(po) && !SupplierPaymentCompletion.CanReopen(po))
                        throw new SupplierCreditException($"{po.PONumber} has moved past payment ({po.Status}); the credit can no longer be taken back.");
                }

                PaymentTransaction? refund = null;
                if (original.Source is SupplierCreditSources.WalletTopUp or SupplierCreditSources.Repayment)
                {
                    var topUp = await db.Set<PaymentTransaction>().Include(t => t.ToSupplier)
                        .FirstAsync(t => t.Id == original.PaymentTransactionId);
                    refund = new PaymentTransaction
                    {
                        PaymentBoxId = topUp.PaymentBoxId, Type = "Deposit", Amount = topUp.Amount,
                        TxCurrency = topUp.TxCurrency, ExchangeRate = topUp.ExchangeRate,
                        FromType = "SupplierCredit", ToType = "MotherWallet", IsAuto = true,
                        InWallet = topUp.InWallet,   // refund only where the original money left
                        Notes = Truncate(original.Source == SupplierCreditSources.Repayment
                            ? $"Reversed credit-line repayment — {topUp.ToSupplier?.Name}: {notes}"
                            : $"Reversed prepayment credit — {topUp.ToSupplier?.Name}: {notes}", 1000),
                    };
                    db.Add(refund);
                    await db.SaveChangesAsync();
                }

                var entry = Post(account, direction, SupplierCreditSources.Reversal, original.Amount,
                    request.RequestId, userId, e =>
                    {
                        e.PurchaseOrderId = original.PurchaseOrderId;
                        e.PaymentRequestId = original.PaymentRequestId;
                        e.PaymentTransactionId = refund?.Id;
                        e.ReversesEntryId = original.Id;
                        e.CreditMemo = original.CreditMemo;
                        e.Notes = notes;
                    });
                await db.SaveChangesAsync();

                if (po != null && pr != null && !IsClosed(po) && !await WalletInProgressAsync(po.Id))
                {
                    await SupplierPaymentCompletion.ReopenAsync(db, po, pr);
                    await db.SaveChangesAsync();
                }
                return await WriteResultAsync(entry.Id);
            });
    }

    // ── Credit lines (E10) ───────────────────────────────────────────────────

    public Task<SupplierCreditWriteResult> SetCreditLineAsync(SetSupplierCreditLineRequest request, long? userId)
    {
        if (request.CreditLimit < 0 || decimal.Round(request.CreditLimit, 2) != request.CreditLimit)
            throw new SupplierCreditException("Enter a limit of 0 or more USD, with at most two decimal places.");
        var notes = Clean(request.Notes, 500, "note");

        return WriteAsync(request.RequestId,
            existing => Replay(existing, SupplierCreditSources.CreditLimit),
            async _ =>
            {
                var (supplier, preset) = await RequirePartiesAsync(request.SupplierId, request.CompanyPresetId);
                var account = await LockAccountAsync(supplier.Id, preset.Id, SupplierCreditAccountKinds.CreditLine);
                if (account == null)
                {
                    account = new SupplierCreditAccount
                    {
                        SupplierId = supplier.Id, CompanyPresetId = preset.Id, Currency = Currency,
                        Kind = SupplierCreditAccountKinds.CreditLine, CreditLimit = 0,
                    };
                    db.Add(account);
                    await db.SaveChangesAsync();
                }

                var delta = request.CreditLimit - (account.CreditLimit ?? 0);
                if (delta == 0)
                    throw new SupplierCreditException($"The credit limit is already {request.CreditLimit:0.00} {Currency}.");
                if (account.Balance + delta < 0)
                    throw new SupplierCreditException(
                        $"{account.Owed:0.00} {Currency} is owed on this line; the limit cannot go below that. Pay it back first.");

                // The limit moves first so Post's "never above the limit" check sees the new limit.
                account.CreditLimit = request.CreditLimit;
                var entry = Post(account, delta > 0 ? SupplierCreditDirections.In : SupplierCreditDirections.Out,
                    SupplierCreditSources.CreditLimit, Math.Abs(delta), request.RequestId, userId, e =>
                        e.Notes = notes ?? $"Credit limit set to {request.CreditLimit:0.00} {Currency}");
                await db.SaveChangesAsync();
                return await WriteResultAsync(entry.Id);
            });
    }

    public async Task<List<SupplierCreditRepaymentRow>?> GetRepaymentsAsync(long accountId)
    {
        if (!await db.Set<SupplierCreditAccount>().AnyAsync(a => a.Id == accountId)) return null;
        var ids = await db.Set<PaymentRequest>().AsNoTracking()
            .Where(p => p.SupplierCreditAccountId == accountId)
            .OrderByDescending(p => p.Id).Select(p => p.Id).ToListAsync();
        return await RepaymentRowsAsync(ids);
    }

    public Task<SupplierCreditRepaymentRow> SaveRepaymentDraftAsync(long accountId, SaveSupplierCreditRepaymentRequest request, long? userId)
    {
        ValidateAmount(request.Amount);
        return db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var account = await RequireCreditLineAsync(accountId);
            if (request.Amount > account.Owed)
                throw new SupplierCreditException($"Only {account.Owed:0.00} {Currency} is owed on this credit line.");
            var wallet = await db.Set<PaymentBox>().FirstOrDefaultAsync(w => w.Id == request.WalletId)
                ?? throw new SupplierCreditException("Select the wallet the repayment is paid from.");
            var rate = WalletRate(wallet, request.ExchangeRate);

            // One open repayment per credit line, like one open POP per PO.
            var pr = await db.Set<PaymentRequest>()
                .Where(p => p.SupplierCreditAccountId == accountId && p.Status != Paid)
                .OrderByDescending(p => p.Id).FirstOrDefaultAsync();
            if (pr?.Status == WaitingForFinalAmount)
                throw new SupplierCreditException("Save the bank's final amount for the current repayment POP first.");
            if (pr == null)
            {
                var maxPrId = await db.Set<PaymentRequest>().MaxAsync(x => (long?)x.PRId) ?? 1500;
                pr = new PaymentRequest
                {
                    PRId = maxPrId + 1, POId = null, SupplierCreditAccountId = accountId,
                    CompanyPresetId = account.CompanyPresetId, IsActive = true, CreatedAt = DateTime.UtcNow,
                };
                db.Add(pr);
            }
            pr.Amount = request.Amount;
            pr.PendingWalletId = wallet.Id;
            pr.PendingPaymentAmount = request.Amount;
            pr.PendingExchangeRate = rate;
            pr.PendingUploadId ??= Guid.NewGuid();
            pr.Status = ReadyForPop;
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return (await RepaymentRowsAsync([pr.Id]))[0];
        });
    }

    public async Task<SupplierCreditWriteResult> UploadRepaymentPopAsync(long accountId, long paymentRequestId, SupplierCreditUpload pop, long? userId)
    {
        ValidateUploads([pop]);
        var draft = await db.Set<PaymentRequest>().AsNoTracking()
            .Where(p => p.Id == paymentRequestId && p.SupplierCreditAccountId == accountId)
            .Select(p => new { p.PendingUploadId })
            .FirstOrDefaultAsync() ?? throw new KeyNotFoundException("Repayment not found.");
        if (draft.PendingUploadId is not { } uploadId)
            throw new SupplierCreditException("Save the repayment details (amount and wallet) before uploading the POP.");

        // The upload id is the idempotency key: a retried upload returns the entry it already wrote.
        return await WriteAsync(uploadId,
            existing => Replay(existing, SupplierCreditSources.Repayment),
            async onFailure =>
            {
                var account = await RequireCreditLineAsync(accountId);
                var pr = await db.Set<PaymentRequest>().FirstAsync(p => p.Id == paymentRequestId);
                if (pr.Status != ReadyForPop || pr.PendingWalletId is null || pr.PendingPaymentAmount is null)
                    throw new SupplierCreditException("Save the repayment details (amount and wallet) before uploading the POP.");
                var amount = pr.PendingPaymentAmount.Value;
                if (amount > account.Owed)
                    throw new SupplierCreditException($"Only {account.Owed:0.00} {Currency} is owed now; save the repayment details again.");
                var wallet = await db.Set<PaymentBox>().FirstOrDefaultAsync(w => w.Id == pr.PendingWalletId.Value)
                    ?? throw new SupplierCreditException("The saved wallet no longer exists. Save the repayment details again.");
                var rate = WalletRate(wallet, pr.PendingExchangeRate);
                var supplier = await db.Set<Supplier>().AsNoTracking().FirstAsync(x => x.Id == account.SupplierId);
                var company = await db.Set<CompanyPreset>().AsNoTracking().Where(c => c.Id == account.CompanyPresetId).Select(c => c.Name).FirstAsync();

                string savedName;
                using (var content = pop.OpenRead())
                    savedName = storage.SaveFileInSupplierCategory(DocumentRoot, supplier.Name, PopFolder,
                        $"PR-{pr.PRId}-{uploadId:N}-{Path.GetFileName(pop.FileName)}", content);
                onFailure.Add(() => storage.DeleteFileInSupplierCategory(DocumentRoot, supplier.Name, PopFolder, savedName));

                var withdrawal = new PaymentTransaction
                {
                    PaymentBoxId = wallet.Id, PaymentRequestId = pr.Id, Amount = amount, TxCurrency = Currency, ExchangeRate = rate,
                    Type = "Withdraw", FromType = "MotherWallet", ToType = "SupplierCredit", ToSupplierId = supplier.Id, IsAuto = true,
                    InWallet = await WalletImport.IsOnAsync(db),
                    PopUploadId = uploadId, PopFileName = savedName, PopInvoiceNumber = DocumentRoot,
                    Notes = Truncate($"Credit line repayment PR-{pr.PRId} — {supplier.Name} / {company}", 1000),
                };
                db.Add(withdrawal);
                await db.SaveChangesAsync();

                // L2: the credit comes back when the money leaves (the POP), not at the final bank amount.
                var entry = Post(account, SupplierCreditDirections.In, SupplierCreditSources.Repayment, amount, uploadId, userId, e =>
                {
                    e.PaymentTransactionId = withdrawal.Id;
                    e.Notes = $"Paid back with PR-{pr.PRId}";
                });
                pr.Status = WaitingForFinalAmount;
                await db.SaveChangesAsync();
                return await WriteResultAsync(entry.Id);
            });
    }

    public Task<SupplierCreditRepaymentRow> FinalizeRepaymentAsync(long accountId, long paymentRequestId, FinalizeSupplierCreditRepaymentRequest request)
    {
        if (request.FinalWalletAmount <= 0 || decimal.Round(request.FinalWalletAmount, 2) != request.FinalWalletAmount)
            throw new SupplierCreditException("Enter the bank's final withdrawn amount with at most two decimal places.");
        return db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var pr = await db.Set<PaymentRequest>()
                .FirstOrDefaultAsync(p => p.Id == paymentRequestId && p.SupplierCreditAccountId == accountId)
                ?? throw new KeyNotFoundException("Repayment not found.");
            if (pr.Status != WaitingForFinalAmount)
                throw new SupplierCreditException("There is no repayment POP waiting for a final bank amount.");
            var withdrawal = await db.Set<PaymentTransaction>().Include(t => t.PaymentBox)
                .FirstOrDefaultAsync(t => t.PopUploadId == pr.PendingUploadId && t.ToType == "SupplierCredit")
                ?? throw new SupplierCreditException("The POP of this repayment could not be found.");

            // Same rule as a PO payment's final amount (SupplierPaymentsController.FinalizeAmount).
            var initial = Math.Round(withdrawal.Amount * (withdrawal.ExchangeRate ?? 1m), 2, MidpointRounding.AwayFromZero);
            if (request.FinalWalletAmount < initial)
                throw new SupplierCreditException($"The final amount cannot be less than the original wallet debit ({initial:0.00} {withdrawal.PaymentBox.Currency}).");
            var fee = request.FinalWalletAmount - initial;
            if (fee > 0)
            {
                db.Add(new PaymentTransaction
                {
                    PaymentBoxId = withdrawal.PaymentBoxId, PaymentRequestId = pr.Id, Amount = fee, TxCurrency = withdrawal.PaymentBox.Currency,
                    Type = "Withdraw", FromType = "MotherWallet", ToType = "BankFee", IsAuto = true, CreatedAt = DateTime.UtcNow,
                    InWallet = withdrawal.InWallet,
                    Notes = $"Bank Fee and Others for credit line repayment PR-{pr.PRId}",
                });
            }
            pr.Status = Paid;
            pr.PendingWalletId = null;
            pr.PendingPaymentAmount = null;
            pr.PendingExchangeRate = null;
            pr.PendingUploadId = null;
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return (await RepaymentRowsAsync([pr.Id]))[0];
        });
    }

    private async Task<List<SupplierCreditRepaymentRow>> RepaymentRowsAsync(IReadOnlyCollection<long> prIds)
    {
        if (prIds.Count == 0) return [];
        var prs = await db.Set<PaymentRequest>().AsNoTracking().Where(p => prIds.Contains(p.Id)).ToListAsync();
        var txs = await db.Set<PaymentTransaction>().AsNoTracking()
            .Where(t => t.PaymentRequestId.HasValue && prIds.Contains(t.PaymentRequestId.Value)
                && t.Type == "Withdraw" && (t.ToType == "SupplierCredit" || t.ToType == "BankFee"))
            .Select(t => new { t.Id, PrId = t.PaymentRequestId!.Value, t.ToType, t.Amount, t.ExchangeRate, t.PopFileName,
                t.PaymentBoxId, WalletName = t.PaymentBox.Name, t.PaymentBox.Currency })
            .ToListAsync();
        var draftWalletIds = prs.Where(p => p.PendingWalletId.HasValue).Select(p => p.PendingWalletId!.Value).Distinct().ToList();
        var wallets = await db.Set<PaymentBox>().AsNoTracking().Where(b => draftWalletIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => new { b.Name, b.Currency });

        return prIds.Select(id => prs.First(p => p.Id == id)).Select(p =>
        {
            var popTx = txs.FirstOrDefault(t => t.PrId == p.Id && t.ToType == "SupplierCredit");
            var feeTx = txs.FirstOrDefault(t => t.PrId == p.Id && t.ToType == "BankFee");
            var draftWallet = p.PendingWalletId is { } w && wallets.TryGetValue(w, out var dw) ? dw : null;
            return new SupplierCreditRepaymentRow(
                p.Id, p.PRId, p.Status, p.Amount ?? 0,
                popTx?.PaymentBoxId ?? p.PendingWalletId,
                popTx?.WalletName ?? draftWallet?.Name,
                popTx?.Currency ?? draftWallet?.Currency,
                popTx?.ExchangeRate ?? p.PendingExchangeRate,
                popTx?.Id, popTx?.PopFileName,
                popTx == null ? null : Math.Round(popTx.Amount * (popTx.ExchangeRate ?? 1m), 2, MidpointRounding.AwayFromZero),
                feeTx?.Amount,
                p.CreatedAt);
        }).ToList();
    }

    private async Task<SupplierCreditAccount> RequireCreditLineAsync(long accountId)
    {
        var account = await LockAccountAsync(accountId) ?? throw new KeyNotFoundException("Credit line not found.");
        if (account.Kind != SupplierCreditAccountKinds.CreditLine)
            throw new SupplierCreditException("Only a credit line can be paid back.");
        return account;
    }

    /// <summary>Wallet currency per 1 USD; 1 for USD wallets. Same rule as the PO payment draft.</summary>
    private static decimal WalletRate(PaymentBox wallet, decimal? exchangeRate)
    {
        var rate = string.Equals(wallet.Currency, Currency, StringComparison.OrdinalIgnoreCase) ? 1m : exchangeRate;
        if (rate is null or <= 0 || decimal.Round(rate.Value, 6) != rate.Value)
            throw new SupplierCreditException("Enter the wallet currency amount per 1 USD (up to six decimal places).");
        return rate.Value;
    }

    private static bool IsCreditLine(string? kind) => string.Equals(kind, SupplierCreditAccountKinds.CreditLine, StringComparison.OrdinalIgnoreCase);

    private static string RequireKind(string? kind) =>
        string.IsNullOrWhiteSpace(kind) || string.Equals(kind, SupplierCreditAccountKinds.Prepaid, StringComparison.OrdinalIgnoreCase)
            ? SupplierCreditAccountKinds.Prepaid
            : IsCreditLine(kind) ? SupplierCreditAccountKinds.CreditLine
            : throw new SupplierCreditException("Unknown credit kind (use Prepaid or CreditLine).");

    // ── Write plumbing ───────────────────────────────────────────────────────

    /// <summary>
    /// Runs one ledger write in a Serializable transaction (retried on transient errors). A request id
    /// that already produced an entry replays that entry's result instead of writing again.
    /// Actions added to the onFailure list (e.g. deleting a saved file) run if the write does not commit.
    /// </summary>
    private async Task<T> WriteAsync<T>(Guid requestId, Func<SupplierCreditEntry, Task<T>> replay, Func<List<Action>, Task<T>> write)
    {
        if (requestId == Guid.Empty) throw new SupplierCreditException("The request id is missing. Refresh the page and try again.");
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                // Looked up outside the Serializable transaction on purpose: a range lock on the RequestId
                // index would deadlock with a parallel write inserting into the same index gap. A parallel
                // duplicate is still caught by UX_SupplierCreditEntries_RequestId below.
                var existing = await db.Set<SupplierCreditEntry>().AsNoTracking().FirstOrDefaultAsync(e => e.RequestId == requestId);
                if (existing != null) return await replay(existing);

                var onFailure = new List<Action>();
                try
                {
                    await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                    var result = await write(onFailure);
                    await transaction.CommitAsync();
                    return result;
                }
                catch
                {
                    foreach (var undo in onFailure) { try { undo(); } catch { /* best effort */ } }
                    throw;
                }
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new SupplierCreditConflictException("The credit balance changed while saving. Refresh and try again.");
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex, "UX_SupplierCreditEntries_RequestId"))
        {
            // The same request committed in parallel (double click); return what it wrote.
            db.ChangeTracker.Clear();
            return await replay(await db.Set<SupplierCreditEntry>().AsNoTracking().FirstAsync(e => e.RequestId == requestId));
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex, "IX_PaymentTransactions_PopUploadId"))
        {
            // A repayment POP uploaded twice at once: the first one committed the withdrawal and its entry.
            db.ChangeTracker.Clear();
            var committed = await db.Set<SupplierCreditEntry>().AsNoTracking().FirstOrDefaultAsync(e => e.RequestId == requestId)
                ?? throw new SupplierCreditConflictException("This POP is already being saved. Refresh and try again.");
            return await replay(committed);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex, "UX_SupplierCreditEntries_ReversesEntryId"))
        {
            throw new SupplierCreditException("This entry has already been reversed.");
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex, "UX_SupplierCreditAccounts_Supplier_Company_Currency"))
        {
            // Two first-ever credits for the same supplier and company raced to create the account.
            throw new SupplierCreditConflictException("The credit account was just created by another request. Try again.");
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex, string indexName) =>
        ex.InnerException?.Message.Contains(indexName, StringComparison.OrdinalIgnoreCase) == true;

    private async Task<SupplierCreditWriteResult> Replay(SupplierCreditEntry existing, string expectedSource) =>
        existing.Source == expectedSource
            ? await WriteResultAsync(existing.Id)
            : throw new SupplierCreditConflictException("This request id was already used for a different credit operation.");

    /// <summary>Locks the account row for the rest of the transaction (UPDLOCK + HOLDLOCK also locks the key range when it does not exist yet).</summary>
    private Task<SupplierCreditAccount?> LockAccountAsync(long supplierId, long companyPresetId, string kind) =>
        db.Set<SupplierCreditAccount>()
            .FromSqlInterpolated($"SELECT * FROM SupplierCreditAccounts WITH (UPDLOCK, HOLDLOCK) WHERE SupplierId = {supplierId} AND CompanyPresetId = {companyPresetId} AND Currency = {Currency} AND Kind = {kind}")
            .FirstOrDefaultAsync();

    private Task<SupplierCreditAccount?> LockAccountAsync(long accountId) =>
        db.Set<SupplierCreditAccount>()
            .FromSqlInterpolated($"SELECT * FROM SupplierCreditAccounts WITH (UPDLOCK, HOLDLOCK) WHERE Id = {accountId}")
            .FirstOrDefaultAsync();

    private async Task<SupplierCreditAccount> GetOrCreatePrepaidAccountAsync(long supplierId, long companyPresetId)
    {
        var account = await LockAccountAsync(supplierId, companyPresetId, SupplierCreditAccountKinds.Prepaid);
        if (account != null) return account;
        account = new SupplierCreditAccount
        {
            SupplierId = supplierId, CompanyPresetId = companyPresetId, Currency = Currency, Kind = SupplierCreditAccountKinds.Prepaid,
        };
        db.Add(account);
        await db.SaveChangesAsync();
        return account;
    }

    /// <summary>Moves the cached balance and adds the matching ledger line. Never lets the balance go negative.</summary>
    private SupplierCreditEntry Post(SupplierCreditAccount account, string direction, string source, decimal amount,
        Guid requestId, long? userId, Action<SupplierCreditEntry> configure)
    {
        var balance = direction == SupplierCreditDirections.In ? account.Balance + amount : account.Balance - amount;
        if (balance < 0)
            throw new SupplierCreditException($"Only {account.Balance:0.00} {account.Currency} credit is available.");
        if (account.Kind == SupplierCreditAccountKinds.CreditLine && balance > (account.CreditLimit ?? 0))
            throw new SupplierCreditException(
                $"Only {account.Owed:0.00} {account.Currency} is owed on this credit line; it cannot be paid back or restored beyond its limit.");
        account.Balance = balance;
        account.ModifyAt = DateTime.UtcNow;

        var entry = new SupplierCreditEntry
        {
            AccountId = account.Id, Direction = direction, Source = source, Amount = amount, BalanceAfter = balance,
            RequestId = requestId, CreatedByUserId = userId, CreatedAt = DateTime.UtcNow,
        };
        configure(entry);
        db.Add(entry);
        return entry;
    }

    private async Task<(Supplier Supplier, CompanyPreset Preset)> RequirePartiesAsync(long supplierId, long companyPresetId)
    {
        var supplier = await db.Set<Supplier>().AsNoTracking().FirstOrDefaultAsync(s => s.Id == supplierId)
            ?? throw new SupplierCreditException("Select a supplier from the catalog.");
        var preset = await db.Set<CompanyPreset>().AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == companyPresetId && p.IsActive && p.Name != PaymentBoxService.NoCompanyPresetName)
            ?? throw new SupplierCreditException("Select one of our active companies.");
        return (supplier, preset);
    }

    private async Task<SupplierCreditWriteResult> WriteResultAsync(long entryId)
    {
        var entry = (await WithDocumentsAsync([await EntryRows(db.Set<SupplierCreditEntry>().AsNoTracking().Where(e => e.Id == entryId)).FirstAsync()]))[0];
        var balance = await db.Set<SupplierCreditAccount>().Where(a => a.Id == entry.AccountId).Select(a => a.Balance).FirstAsync();
        return new SupplierCreditWriteResult(entry, balance);
    }

    private async Task<ApplySupplierCreditResult> ApplyResultAsync(long entryId, PurchaseOrder po, PaymentRequest pr, SupplierPaymentProgress progress)
    {
        var written = await WriteResultAsync(entryId);
        return new ApplySupplierCreditResult(written.Entry, written.Balance, progress.PoPaid, progress.PoRemaining,
            pr.Status, po.PaymentStatus, progress.Complete);
    }

    // ── Rules ────────────────────────────────────────────────────────────────

    private static bool IsClosed(PurchaseOrder po) => SupplierPaymentCompletion.IsClosed(po);

    private static string? CannotApplyReason(PurchaseOrder po) =>
        IsClosed(po) ? "The PO is cancelled or returned."
        : po.PaymentApproval != "Accepted" ? "The payment request must be accepted before credit can be applied."
        : null;

    /// <summary>The buying company: the PO's own column, else (legacy POs) the company of its preferred wallet.</summary>
    private async Task<long?> EffectiveCompanyAsync(PurchaseOrder po)
    {
        if (po.CompanyPresetId.HasValue) return po.CompanyPresetId;
        if (!po.PreferredWalletId.HasValue) return null;
        return await db.Set<PaymentBox>()
            .Where(b => b.Id == po.PreferredWalletId.Value && b.CompanyPreset.Name != PaymentBoxService.NoCompanyPresetName)
            .Select(b => (long?)b.CompanyPresetId)
            .FirstOrDefaultAsync();
    }

    /// <summary>Saved wallet drafts whose POP is not uploaded yet: not paid, but already promised to the supplier.</summary>
    private Task<Dictionary<long, decimal>> PendingDraftsAsync(long poId) =>
        db.Set<PaymentRequest>()
            .Where(p => p.POId == poId && p.PendingUploadId != null && p.PendingPaymentAmount != null
                && !db.Set<PaymentTransaction>().Any(t => t.PopUploadId == p.PendingUploadId))
            .ToDictionaryAsync(p => p.Id, p => p.PendingPaymentAmount!.Value);

    /// <summary>A draft is saved or a POP waits for its final bank amount.</summary>
    private Task<bool> WalletInProgressAsync(long poId) =>
        db.Set<PaymentRequest>().AnyAsync(p => p.POId == poId && p.PendingUploadId != null);

    private static void ValidateAmount(decimal amount)
    {
        if (amount <= 0 || decimal.Round(amount, 2) != amount)
            throw new SupplierCreditException("Enter a positive USD amount with at most two decimal places.");
    }

    private static string? Clean(string? value, int max, string field)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > max) throw new SupplierCreditException($"The {field} cannot exceed {max} characters.");
        return trimmed;
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    // ── Queries ──────────────────────────────────────────────────────────────

    private static IQueryable<SupplierCreditAccount> ApplyFilters(IQueryable<SupplierCreditAccount> query,
        SupplierCreditAccountQuery filter, string? exclude = null)
    {
        if (exclude != "supplier" && filter.SupplierIds is { Count: > 0 } supplierIds)
            query = query.Where(a => supplierIds.Contains(a.SupplierId));
        if (exclude != "company" && filter.CompanyPresetIds is { Count: > 0 } companyIds)
            query = query.Where(a => companyIds.Contains(a.CompanyPresetId));
        var kind = RequireKind(filter.Kind);
        query = query.Where(a => a.Kind == kind);
        if (kind == SupplierCreditAccountKinds.Prepaid && filter.OnlyWithBalance)
            query = query.Where(a => a.Balance > 0);
        if (kind == SupplierCreditAccountKinds.CreditLine && filter.OnlyOwing)
            query = query.Where(a => (a.CreditLimit ?? 0) - a.Balance > 0);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(a => a.Supplier.Name.Contains(term) || a.CompanyPreset.Name.Contains(term));
        }
        return query;
    }

    private static IQueryable<SupplierCreditAccount> Sort(IQueryable<SupplierCreditAccount> query, SupplierCreditAccountQuery filter)
    {
        string key;
        bool desc;
        if (!string.IsNullOrWhiteSpace(filter.SortBy))
        {
            key = filter.SortBy.Trim();
            desc = filter.SortDesc;
        }
        else
        {
            var token = string.IsNullOrWhiteSpace(filter.Sort) ? (IsCreditLine(filter.Kind) ? "-owed" : "-balance") : filter.Sort.Trim();
            desc = token.StartsWith('-');
            key = token.TrimStart('-');
        }

        IOrderedQueryable<SupplierCreditAccount> ordered = key.ToLowerInvariant() switch
        {
            "supplier" or "suppliername" => desc ? query.OrderByDescending(a => a.Supplier.Name) : query.OrderBy(a => a.Supplier.Name),
            "company" or "companypresetname" => desc ? query.OrderByDescending(a => a.CompanyPreset.Name) : query.OrderBy(a => a.CompanyPreset.Name),
            "totalin" => desc
                ? query.OrderByDescending(a => a.Entries.Where(e => e.Direction == SupplierCreditDirections.In).Sum(e => e.Amount))
                : query.OrderBy(a => a.Entries.Where(e => e.Direction == SupplierCreditDirections.In).Sum(e => e.Amount)),
            "totalout" => desc
                ? query.OrderByDescending(a => a.Entries.Where(e => e.Direction == SupplierCreditDirections.Out).Sum(e => e.Amount))
                : query.OrderBy(a => a.Entries.Where(e => e.Direction == SupplierCreditDirections.Out).Sum(e => e.Amount)),
            "owed" => desc
                ? query.OrderByDescending(a => (a.CreditLimit ?? 0) - a.Balance)
                : query.OrderBy(a => (a.CreditLimit ?? 0) - a.Balance),
            "creditlimit" => desc ? query.OrderByDescending(a => a.CreditLimit) : query.OrderBy(a => a.CreditLimit),
            "lastentryat" => desc
                ? query.OrderByDescending(a => a.Entries.Max(e => (DateTime?)e.CreatedAt))
                : query.OrderBy(a => a.Entries.Max(e => (DateTime?)e.CreatedAt)),
            _ => desc ? query.OrderByDescending(a => a.Balance) : query.OrderBy(a => a.Balance),
        };
        return ordered.ThenBy(a => a.Supplier.Name).ThenBy(a => a.Id);
    }

    private IQueryable<SupplierCreditAccountRow> AccountRows(IQueryable<SupplierCreditAccount> query) =>
        query.Select(a => new SupplierCreditAccountRow(
            a.Id,
            a.SupplierId,
            a.Supplier.Name,
            a.CompanyPresetId,
            a.CompanyPreset.Name,
            a.Currency,
            a.Balance,
            a.Entries.Where(e => e.Direction == SupplierCreditDirections.In).Sum(e => e.Amount),
            a.Entries.Where(e => e.Direction == SupplierCreditDirections.Out).Sum(e => e.Amount),
            a.Entries.Max(e => (DateTime?)e.CreatedAt),
            a.Kind,
            a.CreditLimit,
            a.Kind == SupplierCreditAccountKinds.CreditLine ? (a.CreditLimit ?? 0) - a.Balance : 0,
            // First use after the line was last fully available (a hint for the "pay back" reminder).
            a.Kind == SupplierCreditAccountKinds.CreditLine && a.Balance < a.CreditLimit
                ? a.Entries
                    .Where(e => e.Source == SupplierCreditSources.POApplication
                        && e.CreatedAt > (a.Entries.Where(f => f.BalanceAfter == a.CreditLimit).Max(f => (DateTime?)f.CreatedAt) ?? DateTime.MinValue))
                    .Min(e => (DateTime?)e.CreatedAt)
                : null,
            db.Set<PaymentRequest>()
                .Where(p => p.SupplierCreditAccountId == a.Id && p.Status != Paid)
                .OrderByDescending(p => p.Id)
                .Select(p => (long?)p.Id)
                .FirstOrDefault()));

    private static IQueryable<SupplierCreditDocumentRow> DocumentRows(IQueryable<SupplierCreditDocument> query) =>
        query.OrderBy(d => d.Id).Select(d => new SupplierCreditDocumentRow(
            d.Id, d.EntryId, d.Category, d.OriginalFileName, d.SizeBytes, d.UploadedAt,
            d.UploadedBy != null ? d.UploadedBy.Name : null));

    /// <summary>Adds each row's proof documents with one query for the whole page.</summary>
    private async Task<List<SupplierCreditEntryRow>> WithDocumentsAsync(List<SupplierCreditEntryRow> rows)
    {
        if (rows.Count == 0) return rows;
        var ids = rows.Select(r => r.Id).ToList();
        var docs = (await DocumentRows(db.Set<SupplierCreditDocument>().AsNoTracking().Where(d => ids.Contains(d.EntryId))).ToListAsync())
            .ToLookup(d => d.EntryId);
        return rows.Select(r => r with { Documents = docs[r.Id].ToList() }).ToList();
    }

    private IQueryable<SupplierCreditEntryRow> EntryRows(IQueryable<SupplierCreditEntry> query) =>
        query.Select(e => new SupplierCreditEntryRow(
            e.Id,
            e.AccountId,
            e.Direction,
            e.Source,
            e.Amount,
            e.BalanceAfter,
            e.PurchaseOrderId,
            e.PurchaseOrder != null ? e.PurchaseOrder.PONumber : null,
            e.PurchaseOrderId != null ? "/purchase-orders/" + e.PurchaseOrderId : null,
            e.PaymentRequestId,
            e.PaymentRequest != null ? e.PaymentRequest.PRId : null,
            e.PaymentTransactionId,
            e.PaymentTransaction != null ? e.PaymentTransaction.PaymentBox.Name : null,
            e.SourcePurchaseOrderId,
            e.SourcePurchaseOrder != null ? e.SourcePurchaseOrder.PONumber : null,
            e.SourcePurchaseOrderId != null ? "/purchase-orders/" + e.SourcePurchaseOrderId : null,
            e.SourcePoReference,
            e.CreditMemo,
            e.Notes,
            e.ReversesEntryId,
            db.Set<SupplierCreditEntry>().Where(r => r.ReversesEntryId == e.Id).Select(r => (long?)r.Id).FirstOrDefault(),
            e.CreatedAt,
            e.CreatedBy != null ? e.CreatedBy.Name : null));
}
