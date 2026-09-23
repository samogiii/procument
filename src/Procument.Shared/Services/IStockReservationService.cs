namespace Procument.Shared.Services;

/// <summary>
/// Stock reservation boundary used by Sales without taking a dependency on OurInventory.
/// Reservations are keyed by the Sales Order (proforma invoice) line.
/// </summary>
public interface IStockReservationService
{
    Task<StockReservationResult> ReserveAsync(
        long stockItemId,
        decimal quantity,
        long invoiceItemId,
        long createdByUserId,
        CancellationToken cancellationToken = default);

    Task ReleaseAsync(
        long invoiceItemId,
        long closedByUserId,
        CancellationToken cancellationToken = default);

    Task<StockReservationResult> ConsumeAsync(
        long invoiceItemId,
        decimal quantity,
        long closedByUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The Our Stock lot a quote line should be served from, or null when it is not a stock line.
    /// A line is a stock line when it carries a stock lot, or when its supplier is the Our Stock catalog
    /// supplier (then the lot with most available of the same part / condition is used).
    /// </summary>
    Task<long?> ResolveStockLotAsync(
        long? sourceStockItemId,
        long? supplierId,
        long? partNumberId,
        string? condition,
        CancellationToken cancellationToken = default);

    /// <summary>Reserved (not yet issued) and issued quantities for one Sales Order line.</summary>
    Task<StockLineStatus> GetLineStatusAsync(long invoiceItemId, CancellationToken cancellationToken = default);

    /// <summary>Reserved (not yet issued) and issued quantities for a page of Sales Order lines.</summary>
    Task<IReadOnlyDictionary<long, StockLineStatus>> GetLineStatusesAsync(
        IReadOnlyCollection<long> invoiceItemIds,
        CancellationToken cancellationToken = default);

    /// <summary>Id of the Our Stock catalog supplier, when it exists.</summary>
    Task<long?> GetStockSupplierIdAsync(CancellationToken cancellationToken = default);

    /// <summary>Quantity received into Our Stock per Stock PO line (receipts net of corrections).</summary>
    Task<IReadOnlyDictionary<long, decimal>> GetReceivedByPoItemAsync(IReadOnlyCollection<long> poItemIds,
        CancellationToken cancellationToken = default);
}

public sealed record StockReservationResult(
    decimal RequestedQuantity,
    decimal ReservedQuantity,
    decimal RemainingQuantity);

public sealed record StockLineStatus(decimal Reserved, decimal Issued, decimal? IssueCost);
