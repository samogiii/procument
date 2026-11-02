using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Catalog.Entities;
using Procument.Module.Sales.Entities;
using Procument.Module.Sales.Services;
using Procument.Shared.Services;

namespace Procument.Module.Sales.Controllers;

public record SendBackDepositRequest(string? Note);

/// <summary>
/// Wallets page, Incoming Money tab: customer POPs the warehouse has confirmed against the bank (they arrive
/// on the Payment sync, see WarehousePaymentSyncController). Accepting one puts the money in its wallet.
/// </summary>
[Authorize(Roles = "Payment,AHM,Admin,SuperAdmin")]
[ApiController]
[Route("api/incoming-money")]
public class IncomingMoneyController(DbContext db, IDocumentStorageService storage) : ControllerBase
{
    private const int DecidedRows = 200;

    private readonly DbContext _db = db;
    private readonly IDocumentStorageService _storage = storage;

    /// <summary>Everything still open (confirmed by the warehouse first), then the latest accepted.</summary>
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var pending = await _db.Set<IncomingDeposit>().CountAsync(d => d.Status == IncomingDeposit.Pending);
        var rows = await (
            from d in _db.Set<IncomingDeposit>().AsNoTracking()
            join c in _db.Set<Customer>() on d.CustomerPayment.Invoice.CustomerId equals c.Id into customers
            from c in customers.DefaultIfEmpty()
            orderby d.Status == IncomingDeposit.Pending ? 0 : 1, d.BankAt != null ? 0 : 1, d.ReviewedAt ?? d.CreatedAt descending
            select new
            {
                d.Id, d.CustomerPayment.InvoiceId, d.CustomerPayment.Invoice.InvoiceNumber,
                Customer = c != null ? c.CustomerCode : null,
                PopFileName = d.CustomerPayment.FileName, PopNotes = d.CustomerPayment.Notes,
                d.Amount, d.Currency, d.WalletRate, d.OfficeAmount,
                WalletName = d.PaymentBox.Name, WalletCurrency = d.PaymentBox.Currency,
                d.BankPopFileName, d.BankAmount, d.BankRate, d.BankWalletAmount, d.BankBy, d.BankAt, d.RejectNote,
                d.Status, d.BankFee, d.ReviewedAt, d.CreatedAt,
                ReviewedBy = d.ReviewedBy != null ? d.ReviewedBy.Name : null,
            })
            .Take(pending + DecidedRows)
            .ToListAsync();

        return Ok(rows.Select(r => new
        {
            r.Id, r.InvoiceId, r.InvoiceNumber, r.Customer, r.PopFileName, r.PopNotes,
            r.Amount, r.Currency, r.WalletRate, r.OfficeAmount, r.WalletName, r.WalletCurrency,
            r.BankPopFileName, r.BankAmount, r.BankRate, r.BankWalletAmount, r.BankBy, r.BankAt, r.RejectNote,
            r.Status, r.ReviewedAt, r.ReviewedBy, r.CreatedAt,
            Stage = IncomingDeposit.Stage(r.Status, r.BankAt),
            // Before acceptance this is the fee it would book.
            Difference = r.BankWalletAmount is { } received ? IncomingDeposit.Difference(r.OfficeAmount, received) : (decimal?)null,
        }));
    }

    /// <param name="kind">customer = the customer's POP, bank = the bank POP from the warehouse.</param>
    [HttpGet("{id:long}/files/{kind}")]
    public async Task<IActionResult> Download(long id, string kind)
    {
        var row = await _db.Set<IncomingDeposit>().AsNoTracking()
            .Where(d => d.Id == id)
            .Select(d => new { d.CustomerPayment.Invoice.InvoiceNumber, CustomerPop = d.CustomerPayment.FileName, d.BankPopFileName })
            .FirstOrDefaultAsync();
        if (row == null) return NotFound();

        var (folder, name) = kind == "bank"
            ? (IncomingDeposit.BankPopFolder, row.BankPopFileName)
            : (IncomingDeposit.CustomerPopFolder, row.CustomerPop);
        if (name == null) return NotFound();
        var file = _storage.OpenFileInInvoiceCategory(row.InvoiceNumber, folder, name);
        if (file == null) return NotFound();
        new FileExtensionContentTypeProvider().TryGetContentType(name, out var mime);
        return File(file.Value.Stream, mime ?? "application/octet-stream", name);
    }

    /// <summary>
    /// Puts the money in the wallet: the deposit at the Procument value, less a bank fee row of Procument −
    /// warehouse, so the wallet ends on what the bank really received. If more arrived than expected there is
    /// no fee and the deposit is the warehouse's value. Accepting is the user's own decision, so unlike the
    /// automatic rows this does not depend on the Auto Import switch.
    /// </summary>
    [HttpPost("{id:long}/accept")]
    public async Task<IActionResult> Accept(long id)
    {
        return await _db.Database.CreateExecutionStrategy().ExecuteAsync<IActionResult>(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var row = await _db.Set<IncomingDeposit>()
                .Include(d => d.CustomerPayment).ThenInclude(p => p.Invoice)
                .Include(d => d.PaymentBox)
                .FirstOrDefaultAsync(d => d.Id == id);
            if (row == null) return NotFound();
            if (row.Status != IncomingDeposit.Pending) return BadRequest(new { message = "This deposit was already accepted." });
            if (row.BankWalletAmount is not { } received || row.BankAmount is not { } bankAmount)
                return BadRequest(new { message = "The warehouse has not confirmed this money yet." });

            var now = DateTime.UtcNow;
            var userId = CurrentUserId();
            var invoice = row.CustomerPayment.Invoice;
            var wallet = row.PaymentBox;
            var difference = IncomingDeposit.Difference(row.OfficeAmount, received);
            var fee = Math.Max(difference, 0m);
            var bases = await TransactionBases.SuggestAsync(_db, null, invoice.Id, invoice.CustomerId);

            var deposit = new PaymentTransaction
            {
                PaymentBoxId = wallet.Id, Type = "Deposit", FromType = "Customer", FromCustomerId = invoice.CustomerId,
                ToType = "MotherWallet", InvoiceId = invoice.Id,
                Amount = fee > 0 ? row.Amount : bankAmount,
                TxCurrency = row.Currency,
                ExchangeRate = fee > 0 ? row.WalletRate : row.BankRate,
                PopUploadId = row.UploadId, PopFileName = row.CustomerPayment.FileName,
                IsAuto = true, InWallet = true, Base = bases, ReviewedAt = now, ReviewedByUserId = userId,
                Notes = difference < 0
                    ? $"Warehouse received {-difference:0.00} {wallet.Currency} more than the POP on {invoice.InvoiceNumber}"
                    : null,
            };
            _db.Set<PaymentTransaction>().Add(deposit);

            PaymentTransaction? feeRow = null;
            if (fee > 0)
            {
                feeRow = new PaymentTransaction
                {
                    PaymentBoxId = wallet.Id, Type = "Withdraw", FromType = "MotherWallet", ToType = "BankFee",
                    InvoiceId = invoice.Id, Amount = fee, TxCurrency = wallet.Currency,
                    IsAuto = true, InWallet = true, Base = bases, ReviewedAt = now, ReviewedByUserId = userId,
                    Notes = $"Bank fee on {invoice.InvoiceNumber} (Procument {row.OfficeAmount:0.00} − warehouse {received:0.00} {wallet.Currency})",
                };
                _db.Set<PaymentTransaction>().Add(feeRow);
            }
            await _db.SaveChangesAsync();

            row.Status = IncomingDeposit.Accepted;
            row.BankFee = fee;
            row.ReviewedAt = now;
            row.ReviewedByUserId = userId;
            row.RejectNote = null;
            row.DepositTransactionId = deposit.Id;
            row.FeeTransactionId = feeRow?.Id;
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(new { walletName = wallet.Name, walletCurrency = wallet.Currency, received, bankFee = fee });
        });
    }

    /// <summary>Nothing reaches the wallet; the next sync shows the note on the phone so it can be fixed there.</summary>
    [HttpPost("{id:long}/send-back")]
    public async Task<IActionResult> SendBack(long id, [FromBody] SendBackDepositRequest request)
    {
        var note = request.Note?.Trim();
        if (string.IsNullOrEmpty(note)) return BadRequest(new { message = "Say what the warehouse should fix." });
        if (note.Length > 500) note = note[..500];

        var row = await _db.Set<IncomingDeposit>()
            .Include(d => d.CustomerPayment).ThenInclude(p => p.Invoice)
            .FirstOrDefaultAsync(d => d.Id == id);
        if (row == null) return NotFound();
        if (row.Status != IncomingDeposit.Pending) return BadRequest(new { message = "This deposit was already accepted." });
        if (row.BankAt == null) return BadRequest(new { message = "The warehouse has not confirmed this money yet." });

        if (row.BankPopFileName != null)
            _storage.DeleteFileInInvoiceCategory(row.CustomerPayment.Invoice.InvoiceNumber, IncomingDeposit.BankPopFolder, row.BankPopFileName);
        row.BankPopFileName = null;
        row.BankAmount = row.BankRate = row.BankWalletAmount = null;
        row.BankBy = null;
        row.BankAt = null;
        row.RejectNote = note;
        await _db.SaveChangesAsync();
        return Ok(new { sentBack = true });
    }

    private long? CurrentUserId()
        => long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;
}
