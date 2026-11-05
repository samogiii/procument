using Microsoft.EntityFrameworkCore;
using Procument.Data;
using Procument.Module.Purchasing.Entities;
using Procument.Module.Sales.Entities;
using Procument.Module.Sales.Services;
using Procument.Shared.Services;

namespace Procument.API.Services;

/// <summary>
/// Reads waiting POPs (PopReading rows queued by the warehouse import or an office upload) one at a time with the local
/// model, so a slow GPU never holds up a sync. Woken by PopReaderSignal, otherwise it looks every 30 seconds.
/// A failed read is tried again a little later, up to PopReading.MaxAttempts; "Read again" on Wallets starts over.
/// </summary>
public class PopReaderWorker(IServiceScopeFactory scopes, PopReaderSignal signal, IConfiguration config, ILogger<PopReaderWorker> log)
    : BackgroundService
{
    private static readonly TimeSpan Idle = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan AfterFailure = TimeSpan.FromSeconds(20);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromSeconds(10), ct);   // let the app finish starting (migrations etc.)
        while (!ct.IsCancellationRequested)
        {
            bool worked;
            try { worked = await ReadNextAsync(ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception e)
            {
                log.LogError(e, "POP reader round failed");
                worked = false;
            }
            if (!worked)
            {
                try { await signal.WaitAsync(Idle, ct); }
                catch (OperationCanceledException) { return; }
            }
        }
    }

    /// <returns>True when a POP was handled (look again right away).</returns>
    private async Task<bool> ReadNextAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var reader = scope.ServiceProvider.GetRequiredService<PopReader>();

        if (reader.BaseUrl == null)
        {
            // Not configured: say so on the rows instead of leaving them "reading…" forever.
            await db.Set<PopReading>().Where(r => r.Status == PopReading.Waiting)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, PopReading.Off), ct);
            return false;
        }

        var row = await db.Set<PopReading>()
            .Where(r => r.Status == PopReading.Waiting && r.Attempts < PopReading.MaxAttempts)
            .OrderBy(r => r.Attempts).ThenBy(r => r.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (row == null) return false;

        var job = await LoadAsync(db, scope.ServiceProvider, row, ct);
        if (job == null)
        {
            // Accepted or removed meanwhile: nothing to read any more.
            db.Remove(row);
            await db.SaveChangesAsync(ct);
            return true;
        }

        row.Attempts++;
        var started = DateTime.UtcNow;
        try
        {
            var read = await reader.ReadAsync(job.Value.File, job.Value.Name, job.Value.Context, ct);
            row.IsPaymentProof = read.IsPaymentProof;
            row.Amount = Cap(read.Amount);
            row.Currency = read.Currency;
            row.Fees = Cap(read.Fees);
            row.FeesCurrency = read.FeesCurrency;
            row.TotalDebited = Cap(read.TotalDebited);
            row.TotalDebitedCurrency = read.TotalDebitedCurrency;
            row.ReceivedAmount = Cap(read.ReceivedAmount);
            row.ReceivedCurrency = read.ReceivedCurrency;
            row.ExchangeRate = read.ExchangeRate is < 1_000_000 ? read.ExchangeRate : null;
            row.ChargesOption = read.ChargesOption;
            row.ValueDate = read.ValueDate;
            row.Reference = read.Reference;
            row.Confidence = read.Confidence;
            row.Note = read.Note;
            row.RawJson = read.RawJson;
            row.Model = reader.Model.Length > 100 ? reader.Model[..100] : reader.Model;
            row.Error = null;
            row.Status = PopReading.Done;
            row.ReadAt = DateTime.UtcNow;
            log.LogInformation("POP reader: {Kind} #{Id} read in {Seconds:0.0}s: {Amount} {Currency}",
                row.Kind, row.RefId, (DateTime.UtcNow - started).TotalSeconds, row.Amount, row.Currency);
        }
        catch (PopReaderException e)
        {
            row.Error = e.Message.Length > 1000 ? e.Message[..1000] : e.Message;
            if (row.Attempts >= PopReading.MaxAttempts) row.Status = PopReading.Failed;
            log.LogWarning("POP reader: {Kind} #{Id} attempt {Attempt} failed: {Error}", row.Kind, row.RefId, row.Attempts, e.Message);
        }
        await db.SaveChangesAsync(ct);
        if (row.Status == PopReading.Waiting) await Task.Delay(AfterFailure, ct);
        return true;
    }

    /// <summary>Where supplier POPs are filed (SupplierPaymentsController / WarehousePaymentSyncController).</summary>
    private const string SupplierPopFolder = "Our POP to Supplier";

    private static decimal? Cap(decimal? v) => v is > 0 and < 10_000_000_000m ? Math.Round(v.Value, 2, MidpointRounding.AwayFromZero) : null;

    /// <summary>The POP file and what the office expects; null = no POP waiting for acceptance any more.</summary>
    private async Task<(byte[] File, string Name, PopContext Context)?> LoadAsync(AppDbContext db, IServiceProvider sp, PopReading row, CancellationToken ct)
    {
        var root = WalletTransferService.StorageRoot(config);
        switch (row.Kind)
        {
            case PopReading.Transfer:
            {
                var t = await db.Set<WalletTransferPending>().AsNoTracking()
                    .Include(x => x.FromBox).Include(x => x.ToBox)
                    .FirstOrDefaultAsync(x => x.Id == row.RefId, ct);
                if (t is not { Status: WalletTransferService.Review, PopFileName: { } name }) return null;
                var path = WalletTransferService.PopPath(root, t.Id, name);
                if (!File.Exists(path)) throw new PopReaderException("The POP file is missing on disk.");
                return (await File.ReadAllBytesAsync(path, ct), name, new PopContext(row.Kind,
                    PopReader.TransferContext(t.FromBox.Currency, t.ToBox.Currency, t.WithdrawAmount, t.DepositAmount)));
            }
            case PopReading.Withdraw:
            {
                var w = await db.Set<WalletWithdrawPending>().AsNoTracking()
                    .Include(x => x.PaymentBox).Include(x => x.ToSupplier)
                    .FirstOrDefaultAsync(x => x.Id == row.RefId, ct);
                if (w is not { Status: WalletWithdrawPending.Review, PopFileName: { } name }) return null;
                var path = WalletWithdrawService.PopPath(root, w.Id, name);
                if (!File.Exists(path)) throw new PopReaderException("The POP file is missing on disk.");
                var to = w.ToType == "Supplier" ? w.ToSupplier?.Name ?? "a supplier" : "our own mother wallet";
                return (await File.ReadAllBytesAsync(path, ct), name, new PopContext(row.Kind,
                    PopReader.WithdrawContext(w.PaymentBox.Currency, w.Amount, w.Currency ?? w.PaymentBox.Currency, to)));
            }
            case PopReading.Deposit:
            {
                var d = await db.Set<IncomingDeposit>().AsNoTracking()
                    .Include(x => x.CustomerPayment).ThenInclude(p => p.Invoice)
                    .FirstOrDefaultAsync(x => x.Id == row.RefId, ct);
                if (d is not { Status: IncomingDeposit.Pending, BankPopFileName: { } name }) return null;
                var file = sp.GetRequiredService<IDocumentStorageService>()
                    .OpenFileInInvoiceCategory(d.CustomerPayment.Invoice.InvoiceNumber, IncomingDeposit.BankPopFolder, name)
                    ?? throw new PopReaderException("The bank POP file is missing on disk.");
                using var ms = new MemoryStream();
                await using (file.Stream) await file.Stream.CopyToAsync(ms, ct);
                return (ms.ToArray(), name, new PopContext(row.Kind, PopReader.DepositContext(d.Amount, d.Currency, null)));
            }
            case PopReading.Payment:
            {
                // A supplier payment paid on the phone, waiting for "Accept & Finish" on the Payments page.
                var p = await db.Set<PaymentRequest>().AsNoTracking()
                    .Where(x => x.Id == row.RefId && x.WarehousePaidAt != null && x.WarehousePopFileName != null
                        && x.PendingWalletId != null && x.PendingPaymentAmount != null && x.PO != null)
                    .Select(x => new
                    {
                        x.WarehousePopFileName, Amount = x.PendingPaymentAmount!.Value, x.PO!.PONumber, x.PO.InvoiceId,
                        Supplier = x.PO.Supplier.Name,
                        WalletCurrency = db.Set<PaymentBox>().Where(w => w.Id == x.PendingWalletId).Select(w => w.Currency).FirstOrDefault(),
                    })
                    .FirstOrDefaultAsync(ct);
                if (p == null) return null;
                var folder = await db.Set<Invoice>().Where(i => i.Id == p.InvoiceId).Select(i => i.InvoiceNumber).FirstOrDefaultAsync(ct)
                    ?? p.PONumber;
                var file = sp.GetRequiredService<IDocumentStorageService>()
                    .OpenFileInSupplierCategory(folder, p.Supplier, SupplierPopFolder, p.WarehousePopFileName!)
                    ?? throw new PopReaderException("The POP file is missing on disk.");
                using var ms = new MemoryStream();
                await using (file.Stream) await file.Stream.CopyToAsync(ms, ct);
                return (ms.ToArray(), p.WarehousePopFileName!, new PopContext(row.Kind,
                    PopReader.WithdrawContext(p.WalletCurrency ?? "USD", p.Amount, "USD", $"the supplier {p.Supplier}")));
            }
            default:
                return null;
        }
    }
}
