namespace Procument.Shared.Entities;

public class SatelliteNode : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public int BaseNumber { get; set; } // 2 or 5
    public string EndpointUrl { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty; // RSA Public Key
    public string? SharedSecret { get; set; } // Optional AES key
    public DateTime? LastSyncAt { get; set; }
}
