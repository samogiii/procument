namespace Procument.Module.Purchasing.DTOs;

// ── Warehouse ──────────────────────────────────────────
public class SaveWarehouseRequest
{
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Type { get; set; }       // "OurWarehouse" | "Forwarded"
    public string? Address { get; set; }
    public string? ShipToAddress { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? FedexAccount { get; set; }
    public bool? IsActive { get; set; }
}

public class WarehouseResponse
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string Type { get; set; } = "OurWarehouse";
    public string? Address { get; set; }
    public string? ShipToAddress { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? FedexAccount { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class WarehouseUserResponse
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

// ── Shipping / TrackNumberItem ─────────────────────────
public class SubmitTrackNumberItemsRequest
{
    public List<TrackNumberItemInput> Items { get; set; } = new();
}

public class TrackNumberItemInput
{
    public long POItemId { get; set; }
    public int ExpectedQty { get; set; }
    public int? ActualQty { get; set; }
    public bool? IsAvailable { get; set; }
}

public class UpdateTrackNumberItemRequest
{
    public int? ExpectedQty { get; set; }
    public int? ActualQty { get; set; }
    public bool? IsAvailable { get; set; }
}

public class ReviewTrackNumberItemRequest
{
    /// <summary>"Accept" or "Reject"</summary>
    public string Action { get; set; } = "Accept";
    public string? Note { get; set; }
}

public class TrackNumberItemResponse
{
    public long Id { get; set; }
    public long TrackNumberId { get; set; }
    public long POItemId { get; set; }
    public string? PartNumberName { get; set; }
    public int ExpectedQty { get; set; }
    public int? ActualQty { get; set; }
    public bool? IsAvailable { get; set; }
    public string Status { get; set; } = "Pending";
    public string? ReviewNote { get; set; }
    public long? ReviewedByUserId { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<TrackNumberDocumentResponse> Documents { get; set; } = new();
}

// ── TrackNumberDocument ────────────────────────────────
public class TrackNumberDocumentResponse
{
    public long Id { get; set; }
    public long TrackNumberId { get; set; }
    public long? POItemId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string? MimeType { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; }
    public long UploadedByUserId { get; set; }
    public string? UploadedByName { get; set; }
}

// ── Shipping page track summary ────────────────────────
public class ShippingTrackResponse
{
    public long Id { get; set; }
    public string TrackNumber { get; set; } = string.Empty;
    public string? Carrier { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "Active";
    public long? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public long POItemId { get; set; }
    public long POId { get; set; }
    public string? PONumber { get; set; }
    public string? PartNumberName { get; set; }
    /// <summary>Original ordered quantity from the PO item — used as the default Expected Qty.</summary>
    public int PoItemQty { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<TrackNumberItemResponse> Items { get; set; } = new();
    public List<TrackNumberDocumentResponse> Documents { get; set; } = new();
    public List<TrackBoxResponse> Boxes { get; set; } = new();
}

// ── Ready for SN ──────────────────────────────────────
public class ReadyForSnItemResponse
{
    public long TrackNumberItemId { get; set; }
    public long TrackNumberId { get; set; }
    public string TrackNumber { get; set; } = string.Empty;
    public long POItemId { get; set; }
    public string? PartNumberName { get; set; }
    public string? PartDescription { get; set; }
    public string? SupplierName { get; set; }
    public int ActualQty { get; set; }
    public long? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public long? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerCode { get; set; }
}

// ── Track Number Boxes ────────────────────────────────
public class SaveTrackBoxRequest
{
    public int BoxNumber { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? WidthCm { get; set; }
    public decimal? LengthCm { get; set; }
    public string? Notes { get; set; }
}

public class TrackBoxResponse
{
    public long Id { get; set; }
    public long TrackNumberId { get; set; }
    public int BoxNumber { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? WidthCm { get; set; }
    public decimal? LengthCm { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ── Shipment Note Boxes ───────────────────────────────
public class SaveSnBoxRequest
{
    public int BoxNumber { get; set; }
    public long? TrackNumberId { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? WidthCm { get; set; }
    public decimal? LengthCm { get; set; }
    public string? Notes { get; set; }
}

public class SnBoxResponse
{
    public long Id { get; set; }
    public long ShipmentNoteId { get; set; }
    public int BoxNumber { get; set; }
    public long? TrackNumberId { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? WidthCm { get; set; }
    public decimal? LengthCm { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ── Shipment Notes ────────────────────────────────────
public class SnItemSelection
{
    public long TrackNumberItemId { get; set; }
    public bool CertNeeded { get; set; }
}

public class CreateShipmentNoteRequest
{
    public long WarehouseId { get; set; }
    /// <summary>"CPT" | "DDP"</summary>
    public string Type { get; set; } = "DDP";
    public string? TId { get; set; }
    public string? SONumber { get; set; }
    public string? Destination { get; set; }
    public string? AWBNumber { get; set; }
    public List<SnItemSelection> Items { get; set; } = new();
}

public class UpdateShipmentNoteRequest
{
    public string? TId { get; set; }
    public string? SONumber { get; set; }
    public string? Destination { get; set; }
    public string? AWBNumber { get; set; }
    public string? Status { get; set; }
}

public class UpdateShipmentNoteStatusRequest
{
    /// <summary>Target status: "Waiting for Packing" | "Ship To USA" | "Clearing Customs" | "Received in Office" | "Delivered to Customer"</summary>
    public string Status { get; set; } = string.Empty;
}

/// <summary>Used by Inventory users to submit the AWB / carrier tracking number.</summary>
public class UpdateAwbRequest
{
    public string? AWBNumber { get; set; }
}

public class ShipmentNoteResponse
{
    public long Id { get; set; }
    public string SNNumber { get; set; } = string.Empty;
    public long WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    /// <summary>"CPT" | "DDP"</summary>
    public string Type { get; set; } = "DDP";
    public string? TId { get; set; }
    public string? SONumber { get; set; }
    public string? Destination { get; set; }
    public string? AWBNumber { get; set; }
    public string? PdfFileName { get; set; }
    public string Status { get; set; } = "Draft";
    public string? CustomsFileName { get; set; }
    public string? CustomsOriginalFileName { get; set; }
    public DateTime? CustomsUploadedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public long CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public List<ShipmentNoteTrackResponse> TrackNumbers { get; set; } = new();
    public List<SnBoxResponse> Boxes { get; set; } = new();
}

public class ShipmentNoteTrackResponse
{
    public long TrackNumberId { get; set; }
    public string TrackNumber { get; set; } = string.Empty;
    public string? Carrier { get; set; }
    public string Status { get; set; } = "Active";
    public long POItemId { get; set; }
    public string? PartNumberName { get; set; }
    public string? Description { get; set; }
    public string? SupplierName { get; set; }
    public int Qty { get; set; }
    public string? Condition { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerCode { get; set; }
    public List<ShipmentNoteItemResponse> Items { get; set; } = new();
    /// <summary>Boxes received at the warehouse with this track number (entered by Inventory).</summary>
    public List<TrackBoxResponse> ReceivedBoxes { get; set; } = new();
}

public class ShipmentNoteItemResponse
{
    public long TrackNumberItemId { get; set; }
    public long POItemId { get; set; }
    public string? PartNumberName { get; set; }
    public int? ActualQty { get; set; }
    public bool? CertNeeded { get; set; }
    public string Status { get; set; } = "Pending";
}
