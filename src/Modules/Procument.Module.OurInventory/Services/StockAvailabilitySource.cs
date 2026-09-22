using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Services;

namespace Procument.Module.OurInventory.Services;

/// <summary>
/// Foundation implementation. Epic 7 will populate records from OurStockItems and open stock POs.
/// </summary>
public sealed class StockAvailabilitySource : IPartAvailabilitySource
{
    public Task<IReadOnlyList<PartAvailabilitySourceRecord>> GetAvailabilityAsync(
        IReadOnlyCollection<long> partNumberIds,
        bool includeCost,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PartAvailabilitySourceRecord>>([]);
}
