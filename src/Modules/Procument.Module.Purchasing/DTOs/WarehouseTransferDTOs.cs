namespace Procument.Module.Purchasing.DTOs;

// ── Transferable stock (what is sitting at a warehouse, free to move) ──

public class TransferableStockResponse
{
    public long TrackNumberItemId { get; set; }
    public long TrackNumberId { get; set; }
    public string TrackNumber { get; set; } = string.Empty;
    public long POItemId { get; set; }
    public long? POId { get; set; }
    public string? PONumber { get; set; }
    public string? PartNumberName { get; set; }
    public string? PartDescription { get; set; }
    public string? Condition { get; set; }
    public long? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerCode { get; set; }

    /// <summary>Units verified at this warehouse.</summary>
    public int ActualQty { get; set; }
    /// <summary>Units already shipped out on earlier transfers.</summary>
    public int TransferredOutQty { get; set; }
    /// <summary>ActualQty - TransferredOutQty — the most that can be moved now.</summary>
    public int AvailableQty { get; set; }
}

// ── Create ──

public class CreateWarehouseTransferRequest
{
    public long FromWarehouseId { get; set; }
    public long ToWarehouseId { get; set; }
    public string TrackNumber { get; set; } = string.Empty;
    public string? Carrier { get; set; }
    public string? Notes { get; set; }
    public List<WarehouseTransferItemInput> Items { get; set; } = new();
}

public class WarehouseTransferItemInput
{
    public long SourceTrackNumberItemId { get; set; }
    public int Qty { get; set; }
}

// ── Read ──

/// <summary>Bulk receipt of a transfer — every destination leg accepted in one action.</summary>
public class ReceiveTransferRequest
{
    public string? Note { get; set; }
}

public class WarehouseTransferResponse
{
    public long Id { get; set; }
    public string TransferNumber { get; set; } = string.Empty;
    public long FromWarehouseId { get; set; }
    public string? FromWarehouseName { get; set; }
    public long ToWarehouseId { get; set; }
    public string? ToWarehouseName { get; set; }
    public string TrackNumber { get; set; } = string.Empty;
    public string? Carrier { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "In Transit";
    public DateTime CreatedAt { get; set; }
    public long CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public string? ReceivedByName { get; set; }
    public int TotalQty { get; set; }
    public int ReceivedQty { get; set; }
    public List<WarehouseTransferItemResponse> Items { get; set; } = new();
    /// <summary>Destination track-number leg ids emitted by this transfer.</summary>
    public List<long> DestinationTrackNumberIds { get; set; } = new();
}

public class WarehouseTransferItemResponse
{
    public long Id { get; set; }
    public long WarehouseTransferId { get; set; }
    public long SourceTrackNumberItemId { get; set; }
    public long POItemId { get; set; }
    public string? PartNumberName { get; set; }
    public string? PartDescription { get; set; }
    public string? PONumber { get; set; }
    public int Qty { get; set; }
    public int? ReceivedQty { get; set; }
    public string Status { get; set; } = "In Transit";
}

// ── Trace ──

public class ShippingTraceResponse
{
    public long POItemId { get; set; }
    public string? PartNumberName { get; set; }
    public string? PartDescription { get; set; }
    public string? PONumber { get; set; }
    public string? SupplierName { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerCode { get; set; }
    public List<ShippingTraceLeg> Legs { get; set; } = new();
    /// <summary>True once the goods reached the office or the customer.</summary>
    public bool IsComplete { get; set; }
    public string? FinalStatus { get; set; }
}

public class ShippingTraceLeg
{
    public int Sequence { get; set; }
    /// <summary>"Inbound" (supplier → warehouse) | "Transfer" (warehouse → warehouse) | "Shipment" (warehouse → office/customer)</summary>
    public string LegType { get; set; } = "Inbound";
    public string? FromName { get; set; }
    public string? ToName { get; set; }
    public long? FromWarehouseId { get; set; }
    public long? ToWarehouseId { get; set; }
    public string? TrackNumber { get; set; }
    public string? Carrier { get; set; }
    /// <summary>WT-2026-001 or SN-2026-014, when the leg has its own document.</summary>
    public string? Reference { get; set; }
    public long? ReferenceId { get; set; }
    public long? TrackNumberId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? Qty { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ActorName { get; set; }
    public string? Notes { get; set; }
    public bool IsTerminal { get; set; }
}
