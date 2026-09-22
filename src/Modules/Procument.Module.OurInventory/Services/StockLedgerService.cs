using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Procument.Module.OurInventory.Entities;

namespace Procument.Module.OurInventory.Services;

/// <summary>Identifies one stock lot. CertName is part of the key, so a certified and an uncertified unit never mix.</summary>
public sealed record StockLotKey(long PartNumberId, string Condition, long WarehouseId, long CompanyPresetId, string? CertName);

/// <summary>Who/why/what-for of a movement. Copied onto every ledger row it produces.</summary>
public sealed class StockMovementContext
{
    public long UserId { get; init; }
    public string Reference { get; init; } = string.Empty;
    public string? Reason { get; init; }
    public long? POId { get; init; }
    public long? POItemId { get; init; }
    public long? TrackNumberId { get; init; }
    public long? InvoiceItemId { get; init; }
    public long? QuoteItemId { get; init; }
    public long? RFQItemId { get; init; }
    public long? WarehouseTransferId { get; init; }
}

public interface IStockLedgerService
{
    /// <summary>Runs <paramref name="work"/> in one retriable transaction, or inline when a transaction is already open.</summary>
    Task<T> ExecuteAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken = default);

    Task<OurStockItem> GetOrCreateLotAsync(StockLotKey key, CancellationToken cancellationToken = default);
    Task<OurStockItem> LockLotAsync(long stockItemId, CancellationToken cancellationToken = default);

    /// <summary>Inbound movement (Receipt, Opening, CustomerReturn). Recomputes the moving-average cost.</summary>
    Task<OurStockMovement> ReceiveAsync(OurStockItem lot, decimal qty, decimal unitCost, string type,
        StockMovementContext context, IReadOnlyCollection<string>? serials = null, CancellationToken cancellationToken = default);

    Task<OurStockMovement> AdjustAsync(long stockItemId, decimal signedQty, StockMovementContext context,
        CancellationToken cancellationToken = default);

    /// <summary>Outbound movement that is not covered by a reservation (it may only take unreserved stock).</summary>
    Task<OurStockMovement> IssueAsync(long stockItemId, decimal qty, string type, StockMovementContext context,
        CancellationToken cancellationToken = default);

    Task<(OurStockMovement Out, OurStockMovement In)> TransferAsync(long stockItemId, long targetWarehouseId, decimal qty,
        StockMovementContext context, CancellationToken cancellationToken = default);

    /// <summary>Holds stock. With <paramref name="allowPartial"/> it reserves what is available and returns null when nothing is.</summary>
    Task<OurStockReservation?> ReserveAsync(long stockItemId, decimal qty, StockMovementContext context, DateTime? expiresAt,
        bool allowPartial, CancellationToken cancellationToken = default);

    Task ReleaseAsync(long reservationId, string status, CancellationToken cancellationToken = default);

    /// <summary>Issues up to <paramref name="qty"/> from a reservation. The unused part stays reserved.</summary>
    Task<OurStockMovement> ConsumeAsync(long reservationId, decimal qty, StockMovementContext context,
        CancellationToken cancellationToken = default);

    Task<int> AddSerialsAsync(long movementId, IReadOnlyCollection<string> serials, CancellationToken cancellationToken = default);
}

/// <summary>
/// The only code that changes <see cref="OurStockItem.QtyOnHand"/> / <see cref="OurStockItem.QtyReserved"/>.
/// Every quantity change writes a ledger row in the same transaction. Lots are read with UPDLOCK, so two
/// concurrent transactions queue on the row instead of both spending the same quantity; the rowversion
/// column remains as a backstop for any write that bypasses the lock.
/// </summary>
public sealed class StockLedgerService(DbContext db, IOptions<OurInventoryOptions> options) : IStockLedgerService
{
    private bool AllowNegative => options.Value.AllowNegativeStock;

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken = default)
    {
        if (db.Database.CurrentTransaction is not null) return await work();

        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
        {
            // A retry must start from a clean context, otherwise stale lots are written back.
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var result = await work();
            await transaction.CommitAsync(ct);
            return result;
        }, cancellationToken);
    }

    public async Task<OurStockItem> GetOrCreateLotAsync(StockLotKey key, CancellationToken cancellationToken = default)
    {
        EnsureTransaction();
        var condition = key.Condition.Trim();
        var cert = string.IsNullOrWhiteSpace(key.CertName) ? null : key.CertName.Trim();

        // HOLDLOCK takes a key-range lock on the unique lot index, so a concurrent receipt of the
        // same new lot waits here instead of failing on the unique index.
        var lot = await db.Set<OurStockItem>()
            .FromSqlInterpolated($@"SELECT * FROM OurStockItems WITH (UPDLOCK, HOLDLOCK)
                WHERE PartNumberId = {key.PartNumberId} AND Condition = {condition}
                  AND WarehouseId = {key.WarehouseId} AND CompanyPresetId = {key.CompanyPresetId}
                  AND ((CertName IS NULL AND {cert} IS NULL) OR CertName = {cert})")
            .FirstOrDefaultAsync(cancellationToken);
        if (lot is not null)
        {
            await db.Entry(lot).ReloadAsync(cancellationToken);
            return lot;
        }

        lot = new OurStockItem
        {
            PartNumberId = key.PartNumberId,
            Condition = condition,
            WarehouseId = key.WarehouseId,
            CompanyPresetId = key.CompanyPresetId,
            CertName = cert,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Set<OurStockItem>().Add(lot);
        await db.SaveChangesAsync(cancellationToken);
        return lot;
    }

    public async Task<OurStockItem> LockLotAsync(long stockItemId, CancellationToken cancellationToken = default)
    {
        EnsureTransaction();
        var lot = await db.Set<OurStockItem>()
            .FromSqlInterpolated($"SELECT * FROM OurStockItems WITH (UPDLOCK, ROWLOCK) WHERE Id = {stockItemId}")
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Stock item {stockItemId} was not found.");
        // Identity resolution returns an already-tracked instance unchanged; refresh it under the lock.
        await db.Entry(lot).ReloadAsync(cancellationToken);
        return lot;
    }

    public async Task<OurStockMovement> ReceiveAsync(OurStockItem lot, decimal qty, decimal unitCost, string type,
        StockMovementContext context, IReadOnlyCollection<string>? serials = null, CancellationToken cancellationToken = default)
    {
        EnsureTransaction();
        RequirePositive(qty);
        if (unitCost < 0) throw new InvalidOperationException("Unit cost cannot be negative.");

        lot.AvgUnitCost = lot.QtyOnHand <= 0
            ? unitCost
            : Math.Round((lot.QtyOnHand * lot.AvgUnitCost + qty * unitCost) / (lot.QtyOnHand + qty), 4, MidpointRounding.AwayFromZero);
        lot.QtyOnHand += qty;
        lot.UpdatedAt = DateTime.UtcNow;

        var movement = AddMovement(lot, type, qty, unitCost, context);
        await db.SaveChangesAsync(cancellationToken);
        if (serials is { Count: > 0 }) await AddSerialsAsync(movement.Id, serials, cancellationToken);
        return movement;
    }

    public async Task<OurStockMovement> AdjustAsync(long stockItemId, decimal signedQty, StockMovementContext context,
        CancellationToken cancellationToken = default)
    {
        if (signedQty == 0) throw new InvalidOperationException("Adjustment quantity cannot be zero.");
        RequirePrecision(signedQty);
        if (string.IsNullOrWhiteSpace(context.Reason)) throw new InvalidOperationException("A reason is required for a stock adjustment.");

        var lot = await LockLotAsync(stockItemId, cancellationToken);
        if (signedQty < 0 && !AllowNegative && lot.QtyOnHand + signedQty < lot.QtyReserved)
            throw new InvalidOperationException(
                $"Cannot remove {-signedQty:0.##}: only {lot.QtyOnHand - lot.QtyReserved:0.##} is unreserved on this lot.");

        lot.QtyOnHand += signedQty;
        lot.UpdatedAt = DateTime.UtcNow;
        var movement = AddMovement(lot, OurStockMovementTypes.Adjust, signedQty, lot.AvgUnitCost, context);
        await db.SaveChangesAsync(cancellationToken);
        return movement;
    }

    public async Task<OurStockMovement> IssueAsync(long stockItemId, decimal qty, string type, StockMovementContext context,
        CancellationToken cancellationToken = default)
    {
        RequirePositive(qty);
        var lot = await LockLotAsync(stockItemId, cancellationToken);
        if (!AllowNegative && lot.QtyOnHand - lot.QtyReserved < qty)
            throw new InvalidOperationException(
                $"Cannot issue {qty:0.##}: only {lot.QtyOnHand - lot.QtyReserved:0.##} is available on this lot.");

        lot.QtyOnHand -= qty;
        lot.UpdatedAt = DateTime.UtcNow;
        var movement = AddMovement(lot, type, -qty, lot.AvgUnitCost, context);
        await db.SaveChangesAsync(cancellationToken);
        return movement;
    }

    public async Task<(OurStockMovement Out, OurStockMovement In)> TransferAsync(long stockItemId, long targetWarehouseId,
        decimal qty, StockMovementContext context, CancellationToken cancellationToken = default)
    {
        var source = await LockLotAsync(stockItemId, cancellationToken);
        if (source.WarehouseId == targetWarehouseId)
            throw new InvalidOperationException("The target warehouse is the same as the source warehouse.");

        var cost = source.AvgUnitCost;
        var outMovement = await IssueAsync(stockItemId, qty, OurStockMovementTypes.TransferOut, context, cancellationToken);
        var target = await GetOrCreateLotAsync(
            new StockLotKey(source.PartNumberId, source.Condition, targetWarehouseId, source.CompanyPresetId, source.CertName),
            cancellationToken);
        target.TagDate ??= source.TagDate;
        var inMovement = await ReceiveAsync(target, qty, cost, OurStockMovementTypes.TransferIn, context,
            cancellationToken: cancellationToken);
        return (outMovement, inMovement);
    }

    public async Task<OurStockReservation?> ReserveAsync(long stockItemId, decimal qty, StockMovementContext context,
        DateTime? expiresAt, bool allowPartial, CancellationToken cancellationToken = default)
    {
        RequirePositive(qty);
        var lot = await LockLotAsync(stockItemId, cancellationToken);
        var available = Math.Max(0, lot.QtyOnHand - lot.QtyReserved);
        var reserveQty = allowPartial ? Math.Min(qty, available) : qty;
        if (reserveQty <= 0) return null;
        if (!AllowNegative && reserveQty > available)
            throw new InvalidOperationException($"Cannot reserve {qty:0.##}: only {available:0.##} is available on this lot.");

        lot.QtyReserved += reserveQty;
        lot.UpdatedAt = DateTime.UtcNow;
        var reservation = new OurStockReservation
        {
            StockItemId = lot.Id,
            Qty = reserveQty,
            Status = OurStockReservationStatuses.Active,
            RFQItemId = context.RFQItemId,
            QuoteItemId = context.QuoteItemId,
            InvoiceItemId = context.InvoiceItemId,
            ExpiresAt = expiresAt,
            CreatedBy = context.UserId,
            CreatedAt = DateTime.UtcNow,
        };
        db.Set<OurStockReservation>().Add(reservation);
        await db.SaveChangesAsync(cancellationToken);
        return reservation;
    }

    public async Task ReleaseAsync(long reservationId, string status, CancellationToken cancellationToken = default)
    {
        EnsureTransaction();
        if (status is not (OurStockReservationStatuses.Released or OurStockReservationStatuses.Expired))
            throw new InvalidOperationException("A reservation can only be released or expired here.");

        var reservation = await db.Set<OurStockReservation>().FirstOrDefaultAsync(r => r.Id == reservationId, cancellationToken)
            ?? throw new InvalidOperationException($"Reservation {reservationId} was not found.");
        if (reservation.Status != OurStockReservationStatuses.Active) return;

        var lot = await LockLotAsync(reservation.StockItemId, cancellationToken);
        lot.QtyReserved = Math.Max(0, lot.QtyReserved - reservation.Qty);
        lot.UpdatedAt = DateTime.UtcNow;
        reservation.Status = status;
        reservation.ClosedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<OurStockMovement> ConsumeAsync(long reservationId, decimal qty, StockMovementContext context,
        CancellationToken cancellationToken = default)
    {
        EnsureTransaction();
        RequirePositive(qty);
        var reservation = await db.Set<OurStockReservation>().FirstOrDefaultAsync(r => r.Id == reservationId, cancellationToken)
            ?? throw new InvalidOperationException($"Reservation {reservationId} was not found.");
        if (reservation.Status != OurStockReservationStatuses.Active)
            throw new InvalidOperationException($"Reservation {reservationId} is {reservation.Status}, not Active.");
        if (qty > reservation.Qty)
            throw new InvalidOperationException($"Cannot consume {qty:0.##}: the reservation holds {reservation.Qty:0.##}.");

        var lot = await LockLotAsync(reservation.StockItemId, cancellationToken);
        if (!AllowNegative && lot.QtyOnHand < qty)
            throw new InvalidOperationException($"Cannot issue {qty:0.##}: only {lot.QtyOnHand:0.##} is on hand.");

        lot.QtyOnHand -= qty;
        lot.QtyReserved = Math.Max(0, lot.QtyReserved - qty);
        lot.UpdatedAt = DateTime.UtcNow;
        reservation.Qty -= qty;
        if (reservation.Qty == 0)
        {
            reservation.Status = OurStockReservationStatuses.Consumed;
            reservation.ClosedAt = DateTime.UtcNow;
        }

        var movement = AddMovement(lot, OurStockMovementTypes.Issue, -qty, lot.AvgUnitCost, new StockMovementContext
        {
            UserId = context.UserId,
            Reference = context.Reference,
            Reason = context.Reason,
            InvoiceItemId = context.InvoiceItemId ?? reservation.InvoiceItemId,
            QuoteItemId = context.QuoteItemId ?? reservation.QuoteItemId,
            RFQItemId = context.RFQItemId ?? reservation.RFQItemId,
        });
        await db.SaveChangesAsync(cancellationToken);
        return movement;
    }

    public async Task<int> AddSerialsAsync(long movementId, IReadOnlyCollection<string> serials, CancellationToken cancellationToken = default)
    {
        EnsureTransaction();
        var cleaned = serials.Select(s => s?.Trim() ?? string.Empty).Where(s => s.Length > 0).ToList();
        if (cleaned.Count == 0) return 0;
        var duplicate = cleaned.GroupBy(s => s, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null) throw new InvalidOperationException($"Serial number {duplicate.Key} is listed twice.");
        if (cleaned.Any(s => s.Length > 100)) throw new InvalidOperationException("Serial numbers are limited to 100 characters.");

        var movement = await db.Set<OurStockMovement>().Include(m => m.StockItem)
            .FirstOrDefaultAsync(m => m.Id == movementId, cancellationToken)
            ?? throw new InvalidOperationException($"Stock movement {movementId} was not found.");
        if (movement.Qty <= 0) throw new InvalidOperationException("Serials can only be attached to an inbound movement.");

        var existingCount = await db.Set<OurStockSerial>().CountAsync(s => s.ReceiptMovementId == movementId, cancellationToken);
        if (existingCount + cleaned.Count > movement.Qty)
            throw new InvalidOperationException(
                $"This receipt is for {movement.Qty:0.##} unit(s) and already has {existingCount} serial(s); {cleaned.Count} more do not fit.");

        var partNumberId = movement.StockItem.PartNumberId;
        var taken = await db.Set<OurStockSerial>()
            .Where(s => s.PartNumberId == partNumberId && cleaned.Contains(s.SerialNumber))
            .Select(s => s.SerialNumber).FirstOrDefaultAsync(cancellationToken);
        if (taken is not null) throw new InvalidOperationException($"Serial number {taken} is already recorded for this part.");

        db.Set<OurStockSerial>().AddRange(cleaned.Select(serial => new OurStockSerial
        {
            StockItemId = movement.StockItemId,
            PartNumberId = partNumberId,
            SerialNumber = serial,
            Status = OurStockSerialStatuses.InStock,
            ReceiptMovementId = movementId,
        }));
        await db.SaveChangesAsync(cancellationToken);
        return cleaned.Count;
    }

    private OurStockMovement AddMovement(OurStockItem lot, string type, decimal qty, decimal? unitCost, StockMovementContext context)
    {
        if (!AllowNegative && lot.QtyOnHand < 0)
            throw new InvalidOperationException("This movement would make the stock quantity negative.");
        if (context.UserId <= 0) throw new InvalidOperationException("A user is required for stock movements.");

        var movement = new OurStockMovement
        {
            StockItemId = lot.Id,
            Type = type,
            Qty = qty,
            UnitCost = unitCost,
            POId = context.POId,
            POItemId = context.POItemId,
            TrackNumberId = context.TrackNumberId,
            InvoiceItemId = context.InvoiceItemId,
            QuoteItemId = context.QuoteItemId,
            RFQItemId = context.RFQItemId,
            WarehouseTransferId = context.WarehouseTransferId,
            Reference = Truncate(context.Reference, 100),
            Reason = string.IsNullOrWhiteSpace(context.Reason) ? null : Truncate(context.Reason.Trim(), 200),
            CreatedBy = context.UserId,
            CreatedAt = DateTime.UtcNow,
        };
        db.Set<OurStockMovement>().Add(movement);
        return movement;
    }

    private void EnsureTransaction()
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Stock ledger changes must run inside IStockLedgerService.ExecuteAsync.");
    }

    private static void RequirePositive(decimal qty)
    {
        if (qty <= 0) throw new InvalidOperationException("Quantity must be greater than zero.");
        RequirePrecision(qty);
    }

    private static void RequirePrecision(decimal qty)
    {
        if (decimal.Round(qty, 2) != qty) throw new InvalidOperationException("Quantities are limited to two decimal places.");
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
