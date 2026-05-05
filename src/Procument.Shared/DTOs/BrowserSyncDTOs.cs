namespace Procument.Shared.DTOs;

public class BrowserImportRequest
{
    public long NodeId { get; set; }
    public List<SyncRFQData> RFQs { get; set; } = new();
}

public class BrowserImportResult
{
    public int Created { get; set; }
    public int Updated { get; set; }
    public List<RfqIdMapping> IdMappings { get; set; } = new();
}

public class RfqIdMapping
{
    public long SatelliteId { get; set; }
    public long MainAppId { get; set; }
    public List<ItemIdMapping> Items { get; set; } = new();
}

public class ItemIdMapping
{
    public long SatelliteId { get; set; }
    public long MainAppId { get; set; }
}

// Relayed response from satellite — encrypted blob plus the plain counters satellite attaches outside the ciphertext.
public class SatelliteRelayResponse
{
    public string CipherText { get; set; } = string.Empty;
    public string Iv { get; set; } = string.Empty;
    public int ReceivedCount { get; set; }
    public int PendingCount { get; set; }
    public int TotalSatelliteRfqs { get; set; }
}

// Encrypted package exported by satellite browser for manual import.
public class BrowserSyncPackageRequest
{
    public string CipherText { get; set; } = string.Empty;
    public string Iv { get; set; } = string.Empty;
}

// SharedKey removed — crypto is now server-side only.
public class BrowserSyncConfigResponse
{
    public string SatelliteUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public long DefaultNodeId { get; set; }
}
