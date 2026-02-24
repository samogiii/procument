namespace Procument.Module.Purchasing.DTOs;

// ──── Request DTOs ────

/// <summary>Create PO by assigning existing unassigned POItems to a new PO.</summary>
public class CreatePORequest
{
    public long SupplierId { get; set; }
    public long? InvoiceId { get; set; }
    public List<long> POItemIds { get; set; } = new();
}

/// <summary>Update a single POItem (supplier, qty, unitPrice).</summary>
public class UpdatePOItemRequest
{
    public long Id { get; set; }
    public long? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
}

public class UpdatePOStatusRequest
{
    public string Status { get; set; } = string.Empty;
}

// ──── Response DTOs ────

public class POResponse
{
    public long Id { get; set; }
    public string PONumber { get; set; } = string.Empty;
    public decimal? TotalAmount { get; set; }
    public string Status { get; set; } = "Draft";
    public DateTime CreatedAt { get; set; }
    public long SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public long? InvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public List<POItemResponse> Items { get; set; } = new();
}

public class POItemResponse
{
    public long Id { get; set; }
    public long? POId { get; set; }
    public long? ProcumentId { get; set; }
    public long? PartNumberId { get; set; }
    public string? PartNumberName { get; set; }
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string? Condition { get; set; }
    public long? SupplierId { get; set; }
    public string? SupplierName { get; set; }
}

/// <summary>Unassigned POItem response — enriched with ExType, customer, supplier info.</summary>
public class UnassignedPOItemResponse
{
    public long Id { get; set; }
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string? Condition { get; set; }
    public long? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public long? PartNumberId { get; set; }
    public string? PartNumberName { get; set; }
    public string? Alt { get; set; }
    public long? ProcumentId { get; set; }
    public long? InvoiceItemId { get; set; }
    public int? ExType { get; set; }
    public string? CustomerName { get; set; }
    public long? InvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
}
