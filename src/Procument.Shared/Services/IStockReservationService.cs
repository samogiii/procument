namespace Procument.Shared.Services;

/// <summary>
/// Stock reservation boundary used by Sales without taking a dependency on OurInventory.
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
}

public sealed record StockReservationResult(
    decimal RequestedQuantity,
    decimal ReservedQuantity,
    decimal RemainingQuantity);
