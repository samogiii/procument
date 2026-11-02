using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Purchasing.Entities;
using Procument.Module.Purchasing.Services;
using Procument.Module.Sales.Entities;
using Procument.Module.Sales.Services;

namespace Procument.Module.Sales.Controllers;

/// <summary>ExchangeRate = CNY per 1 unit of the wallet currency (not used for CNY wallets).</summary>
public record AcceptShippingCostRequest(long WalletId, decimal? ExchangeRate);
public record RejectShippingCostRequest(string? Note);

/// <summary>
/// Payment page, Shipping Costs tab: costs entered in the warehouse app (they arrive on the Payment sync,
/// see WarehousePaymentSyncController). Accepting one withdraws it from the wallet the user picks as a
/// "Bank Fee and Others" row, which is how the Wallets page and BI already treat shipping costs.
/// </summary>
[Authorize(Roles = "Payment,AHM,Admin,SuperAdmin")]
[ApiController]
[Route("api/shipping-costs")]
public class ShippingCostsController(DbContext db, IShippingService shipping) : ControllerBase
{
    private const string Cny = "CNY";
    private const int DecidedRows = 200;

    private readonly DbContext _db = db;
    private readonly IShippingService _shipping = shipping;

    /// <summary>Everything waiting for a decision, then the latest decided ones.</summary>
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var pending = await _db.Set<WarehouseShippingCost>().CountAsync(c => c.Status == WarehouseShippingCost.Pending);
        var rows = await _db.Set<WarehouseShippingCost>().AsNoTracking()
            .OrderBy(c => c.Status == WarehouseShippingCost.Pending ? 0 : 1)
            .ThenByDescending(c => c.ReviewedAt ?? c.EnteredAt)
            .Take(pending + DecidedRows)
            .Select(c => new
            {
                c.Id, c.TrackNumber, c.Carrier, c.WarehouseName, c.TrackIds, c.FixedCost, c.OtherCost,
                c.EnteredBy, c.EnteredAt, c.Status, c.RejectNote, c.ReviewedAt,
                ReviewedBy = c.ReviewedBy != null ? c.ReviewedBy.Name : null,
                WalletName = c.PaymentBox != null ? c.PaymentBox.Name : null,
                WalletCurrency = c.PaymentBox != null ? c.PaymentBox.Currency : null,
                c.ExchangeRate, c.WalletAmount,
            })
            .ToListAsync();

        var lines = await TrackLinesAsync(rows.SelectMany(r => ParseIds(r.TrackIds)).Distinct().ToList());
        var invoiceIds = lines.Where(l => l.InvoiceId != null).Select(l => l.InvoiceId!.Value).Distinct().ToList();
        var salesOrders = await _db.Set<Invoice>().AsNoTracking().Where(i => invoiceIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.InvoiceNumber);

        return Ok(rows.Select(r =>
        {
            var ids = ParseIds(r.TrackIds);
            var own = lines.Where(l => ids.Contains(l.TrackId)).ToList();
            return new
            {
                r.Id, r.TrackNumber, r.Carrier, r.WarehouseName, r.FixedCost, r.OtherCost,
                Total = r.FixedCost + r.OtherCost, Currency = Cny,
                r.EnteredBy, r.EnteredAt, r.Status, r.RejectNote, r.ReviewedAt, r.ReviewedBy,
                r.WalletName, r.WalletCurrency, r.ExchangeRate, r.WalletAmount,
                Pos = own.Where(l => l.PoId != null).DistinctBy(l => l.PoId).Select(l => new { Id = l.PoId, Number = l.PoNumber }),
                Parts = own.Select(l => l.PartNumber).Where(p => p != null).Distinct(),
                SalesOrders = own.Where(l => l.InvoiceId != null).Select(l => salesOrders.GetValueOrDefault(l.InvoiceId!.Value))
                    .Where(s => s != null).Distinct(),
            };
        }));
    }

    /// <summary>The shipment documents the office has (they arrive on the Shipping page's Sync warehouse).</summary>
    [HttpGet("{id:long}/documents")]
    public async Task<IActionResult> Documents(long id)
    {
        var cost = await _db.Set<WarehouseShippingCost>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (cost == null) return NotFound();
        var ids = ParseIds(cost.TrackIds);
        return Ok(await ShipmentDocs(ids)
            .OrderBy(d => d.UploadedAt)
            .Select(d => new { d.Id, Name = d.OriginalFileName, d.MimeType, d.UploadedAt })
            .ToListAsync());
    }

    [HttpGet("{id:long}/documents/{docId:long}/file")]
    public async Task<IActionResult> DocumentFile(long id, long docId)
    {
        var cost = await _db.Set<WarehouseShippingCost>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (cost == null || !await ShipmentDocs(ParseIds(cost.TrackIds)).AnyAsync(d => d.Id == docId)) return NotFound();
        var file = await _shipping.DownloadDocAsync(docId);
        if (file == null) return NotFound();
        return File(file.Value.stream, file.Value.mimeType, file.Value.fileName);
    }

    /// <summary>Withdraw the cost from the chosen wallet. Like every automatic wallet row, it follows the Auto Import switch.</summary>
    [HttpPost("{id:long}/accept")]
    public async Task<IActionResult> Accept(long id, [FromBody] AcceptShippingCostRequest request)
    {
        return await _db.Database.CreateExecutionStrategy().ExecuteAsync<IActionResult>(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var cost = await _db.Set<WarehouseShippingCost>().FirstOrDefaultAsync(c => c.Id == id);
            if (cost == null) return NotFound();
            if (cost.Status != WarehouseShippingCost.Pending)
                return BadRequest(new { message = $"This shipping cost was already {cost.Status.ToLowerInvariant()}." });
            var wallet = await _db.Set<PaymentBox>().FirstOrDefaultAsync(w => w.Id == request.WalletId);
            if (wallet == null) return BadRequest(new { message = "Select the wallet this cost is withdrawn from." });

            var isCny = string.Equals(wallet.Currency, Cny, StringComparison.OrdinalIgnoreCase);
            var rate = isCny ? null : request.ExchangeRate;
            if (!isCny && (rate is null or <= 0 || decimal.Round(rate.Value, 6) != rate.Value))
                return BadRequest(new { message = $"Enter CNY per 1 {wallet.Currency} (up to six decimal places)." });

            var total = cost.FixedCost + cost.OtherCost;
            var walletAmount = isCny ? total : Math.Round(total / rate!.Value, 2, MidpointRounding.AwayFromZero);
            if (walletAmount <= 0) return BadRequest(new { message = "The amount is too small to withdraw from this wallet." });

            var invoiceIds = (await TrackLinesAsync(ParseIds(cost.TrackIds)))
                .Where(l => l.InvoiceId != null).Select(l => l.InvoiceId!.Value).Distinct().ToList();
            var bases = new List<string?>();
            foreach (var invoiceId in invoiceIds) bases.Add(await TransactionBases.ForInvoiceAsync(_db, invoiceId));

            var rateText = isCny ? "" : $" at {rate} CNY per 1 {wallet.Currency}";
            var payment = new PaymentTransaction
            {
                PaymentBoxId = wallet.Id, Amount = walletAmount, TxCurrency = wallet.Currency,
                Type = "Withdraw", FromType = "MotherWallet", ToType = "BankFee", IsAuto = true,
                // One Sales Order: its cost in BI. A shipment shared by several can't be pinned on one of them.
                InvoiceId = invoiceIds.Count == 1 ? invoiceIds[0] : null,
                InWallet = await WalletImport.IsOnAsync(_db),
                Base = TransactionBases.Normalize(string.Join(",", bases)),
                Notes = $"Shipping cost for {cost.TrackNumber} (fixed {cost.FixedCost:0.00} + others {cost.OtherCost:0.00} CNY{rateText})",
            };
            _db.Set<PaymentTransaction>().Add(payment);
            await _db.SaveChangesAsync();

            cost.Status = WarehouseShippingCost.Accepted;
            cost.ReviewedAt = DateTime.UtcNow;
            cost.ReviewedByUserId = CurrentUserId();
            cost.RejectNote = null;
            cost.PaymentBoxId = wallet.Id;
            cost.ExchangeRate = rate;
            cost.WalletAmount = walletAmount;
            cost.PaymentTransactionId = payment.Id;
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(new { walletName = wallet.Name, walletCurrency = wallet.Currency, walletAmount, inWallet = payment.InWallet });
        });
    }

    /// <summary>Nothing is withdrawn; the next sync shows the note on the phone so it can be fixed there.</summary>
    [HttpPost("{id:long}/reject")]
    public async Task<IActionResult> Reject(long id, [FromBody] RejectShippingCostRequest request)
    {
        var note = request.Note?.Trim();
        if (string.IsNullOrEmpty(note)) return BadRequest(new { message = "Say why the cost is rejected." });
        if (note.Length > 500) note = note[..500];

        var cost = await _db.Set<WarehouseShippingCost>().FirstOrDefaultAsync(c => c.Id == id);
        if (cost == null) return NotFound();
        if (cost.Status != WarehouseShippingCost.Pending)
            return BadRequest(new { message = $"This shipping cost was already {cost.Status.ToLowerInvariant()}." });

        cost.Status = WarehouseShippingCost.Rejected;
        cost.RejectNote = note;
        cost.ReviewedAt = DateTime.UtcNow;
        cost.ReviewedByUserId = CurrentUserId();
        await _db.SaveChangesAsync();
        return Ok(new { rejected = true });
    }

    // ── Helpers ──

    private record TrackLine(long TrackId, long? PoId, string? PoNumber, string? PartNumber, long? InvoiceId);

    /// <summary>A track's PO and part, and the Sales Order of its line (a merged PO can serve several).</summary>
    private Task<List<TrackLine>> TrackLinesAsync(List<long> trackIds) =>
        (from t in _db.Set<POItemTrackNumber>().AsNoTracking()
         where trackIds.Contains(t.Id)
         join ii in _db.Set<InvoiceItem>() on t.POItem.InvoiceItemId equals (long?)ii.Id into invoiceItems
         from ii in invoiceItems.DefaultIfEmpty()
         select new TrackLine(
             t.Id,
             t.POItem.POId,
             t.POItem.PurchaseOrder != null ? t.POItem.PurchaseOrder.PONumber : null,
             t.POItem.PartNumber != null ? t.POItem.PartNumber.Name : null,
             ii != null ? ii.InvoiceId : t.POItem.PurchaseOrder != null ? t.POItem.PurchaseOrder.InvoiceId : null))
        .ToListAsync();

    private IQueryable<TrackNumberDocument> ShipmentDocs(List<long> trackIds) => _db.Set<TrackNumberDocument>().AsNoTracking()
        .Where(d => trackIds.Contains(d.TrackNumberId) && d.POItemId == null);

    private static List<long> ParseIds(string csv) => csv
        .Split(',', StringSplitOptions.RemoveEmptyEntries)
        .Select(v => long.TryParse(v, out var id) ? id : 0)
        .Where(id => id > 0)
        .ToList();

    private long? CurrentUserId()
        => long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;
}
