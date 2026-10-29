namespace Procument.API.Services;

// Wire contract with the warehouse app (procument-warehouse/server/Warehouse.Api/Services/SyncContracts.cs
// keeps the same shapes). Everything travels AES-GCM encrypted; the browser only relays envelopes.

public class WarehouseSyncEnvelope
{
    public string CipherText { get; set; } = string.Empty;
    public string Iv { get; set; } = string.Empty;
}

// ── Office → warehouse ──

public class WarehousePush
{
    public DateTime SentAt { get; set; }
    public List<WarehousePushTrack> Tracks { get; set; } = new();
    public List<WarehousePushBox> Boxes { get; set; } = new();
}

public class WarehousePushTrack
{
    public long MainTrackId { get; set; }
    public string TrackNumber { get; set; } = string.Empty;
    public string? Carrier { get; set; }
    public long? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public long PoItemId { get; set; }
    public string? PoNumber { get; set; }
    public string? PartNumber { get; set; }
    public string? SupplierName { get; set; }
    public int ExpectedQty { get; set; }
    public int? ActualQty { get; set; }
    public string? Status { get; set; }
    public string? ReviewStatus { get; set; }
    public int OfficeFileCount { get; set; }
}

public class WarehousePushBox
{
    public long MainBoxId { get; set; }
    public long MainTrackId { get; set; }
    public int BoxNumber { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? WidthCm { get; set; }
    public decimal? LengthCm { get; set; }
    public string? Notes { get; set; }
}

// ── Warehouse → office ──

public class WarehouseChanges
{
    public List<WarehouseQtyChange> Parts { get; set; } = new();
    public List<WarehouseNoteChange> Notes { get; set; } = new();
    public List<WarehouseBoxChange> Boxes { get; set; } = new();
    public List<WarehouseBoxDelete> DeletedBoxes { get; set; } = new();
    public List<WarehouseFileChange> Files { get; set; } = new();
}

public class WarehouseQtyChange
{
    public long PartId { get; set; }
    public int Version { get; set; }
    public long MainTrackId { get; set; }
    public long PoItemId { get; set; }
    public int ExpectedQty { get; set; }
    public int? ActualQty { get; set; }
    public string? By { get; set; }
}

public class WarehouseNoteChange
{
    public long ShipmentId { get; set; }
    public int Version { get; set; }
    public List<long> MainTrackIds { get; set; } = new();
    public string? Note { get; set; }
    public string? By { get; set; }
}

public class WarehouseBoxChange
{
    public long BoxId { get; set; }
    public int Version { get; set; }
    public long? MainBoxId { get; set; }
    public List<long> MainTrackIds { get; set; } = new();
    public int BoxNumber { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? WidthCm { get; set; }
    public decimal? LengthCm { get; set; }
    public string? Notes { get; set; }
}

public class WarehouseBoxDelete
{
    public long BoxId { get; set; }
    public long MainBoxId { get; set; }
}

public class WarehouseFileChange
{
    public long FileId { get; set; }
    public List<long> MainTrackIds { get; set; } = new();
    public long? PoItemId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = "application/octet-stream";
    public string DataBase64 { get; set; } = string.Empty;
    public string? UploadedBy { get; set; }
}

// ── Office → warehouse: what was stored ──

public class WarehouseAck
{
    public List<WarehouseVersionAck> Parts { get; set; } = new();
    public List<WarehouseVersionAck> Notes { get; set; } = new();
    public List<WarehouseBoxAck> Boxes { get; set; } = new();
    public List<long> DeletedBoxIds { get; set; } = new();
    public List<WarehouseFileAck> Files { get; set; } = new();
}

public class WarehouseVersionAck
{
    public long Id { get; set; }
    public int Version { get; set; }
}

public class WarehouseBoxAck
{
    public long BoxId { get; set; }
    public int Version { get; set; }
    public long MainBoxId { get; set; }
}

public class WarehouseFileAck
{
    public long FileId { get; set; }
    public long MainDocId { get; set; }
}

public class WarehouseImportSummary
{
    public int Quantities { get; set; }
    public int Notes { get; set; }
    public int Boxes { get; set; }
    public int DeletedBoxes { get; set; }
    public int Files { get; set; }
    public List<string> Skipped { get; set; } = new();
}
