namespace Procument.Shared.Services;

/// <summary>
/// Called by the RFQ module after new RFQ items are saved (new RFQ, added item, bulk import),
/// so other modules can react without the RFQ module depending on them. Runs after the save —
/// a failure here never undoes the RFQ.
/// </summary>
public interface IRfqItemsCreatedHandler
{
    Task OnRfqItemsCreatedAsync(IReadOnlyCollection<long> rfqItemIds, long? userId, CancellationToken cancellationToken = default);
}
