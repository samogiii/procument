namespace Procument.Module.Purchasing.Services;

/// <summary>
/// Receives a Purchasing track-number event without making Purchasing depend on a stock module.
/// Implementations must be idempotent: ShippingService calls this after every change to a track's
/// accepted quantities, and runs it inside the same transaction as that change.
/// </summary>
public interface IStockReceiptHandler
{
    Task OnReceivedAsync(long trackNumberId, long userId, CancellationToken cancellationToken = default);
}
