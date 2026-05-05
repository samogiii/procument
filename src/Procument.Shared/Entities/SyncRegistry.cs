namespace Procument.Shared.Entities;

public class SyncRegistry : BaseEntity
{
    public string EntityName { get; set; } = string.Empty; // "RFQ", "RFQItem"
    public long MainAppId { get; set; }
    public long SatelliteAppId { get; set; }
    public long SatelliteNodeId { get; set; }
    public string? LastSyncHash { get; set; }
    public DateTime LastSyncAt { get; set; } = DateTime.UtcNow;

    public virtual SatelliteNode SatelliteNode { get; set; } = null!;
}
