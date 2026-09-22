using Procument.Module.Purchasing.DTOs;

namespace Procument.Module.Purchasing.Services;

/// <summary>
/// Extension point for availability providers that live outside Purchasing.
/// </summary>
public interface IPartAvailabilitySource
{
    Task<IReadOnlyList<PartAvailabilitySourceRecord>> GetAvailabilityAsync(
        IReadOnlyCollection<long> partNumberIds,
        bool includeCost,
        CancellationToken cancellationToken = default);
}
