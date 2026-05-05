namespace Procument.Module.Purchasing.DTOs;

// ──── Request DTOs ────

public class UpdateProcurementItemRequest
{
    public int? Qty { get; set; }
    public decimal? UnitPrice { get; set; }
    public long? CurrentSupplierId { get; set; }
    public string? LeadTime { get; set; }
    public string? Condition { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public string? Alt { get; set; }
    public string? Note { get; set; }
    public string? ItemStatus { get; set; }
}

public class UpsertSupplierQuoteRequest
{
    /// <summary>Id of existing quote to update, or null to create.</summary>
    public long? Id { get; set; }
    public long? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public decimal Price { get; set; }
    public double Qty { get; set; }
    public string? Condition { get; set; }
    public string? Unit { get; set; }
    public string? Alt { get; set; }
    public string? LeadTime { get; set; }
    public string? CertName { get; set; }
    public double? ShippingCost { get; set; }
    public string? Note { get; set; }
    public DateOnly? TagDate { get; set; }
    public string? ShippingPoint { get; set; }
}

public class FinalizeProcurementRequest
{
    public string? Notes { get; set; }
}

/// <summary>
/// Request to return a PO (or a subset of its items) back into the Procurement layer.
/// Empty ItemIds means "return ALL items on this PO" (full return).
/// </summary>
public class ReturnPORequest
{
    public string Reason { get; set; } = string.Empty;
    public List<long>? ItemIds { get; set; }
}

public class ReturnPOResponse
{
    public long POId { get; set; }
    public bool FullReturn { get; set; }
    public string POStatus { get; set; } = string.Empty;
    public List<long> ReturnedPOItemIds { get; set; } = new();
    public List<long> ReopenedProcurementIds { get; set; } = new();
    public List<long> SkippedPOItemIds { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

// ──── Response DTOs ────

public class ProcurementResponse
{
    public long Id { get; set; }
    public string ProcurementNumber { get; set; } = string.Empty;
    public string Status { get; set; } = "Open";
    public DateTime CreatedAt { get; set; }
    public DateTime? FinalizedAt { get; set; }
    public long? CreatedByUserId { get; set; }
    public long? FinalizedByUserId { get; set; }
    public string? Notes { get; set; }

    public long InvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public long? CustomerId { get; set; }
    public string? CustomerName { get; set; }

    public int ItemCount { get; set; }

    public List<ProcurementItemResponse> Items { get; set; } = new();

    /// <summary>Union of EntityPermission rows for Procurement / Quote / RFQ scoped to the source ids.</summary>
    public List<ProcurementAssignedUser> SourceAssignedUsers { get; set; } = new();

    /// <summary>EntityPermission rows with EntityName=Procurement and EntityId=this.Id.</summary>
    public List<ProcurementAssignedUser> AssignedUsers { get; set; } = new();
}

public class ProcurementItemResponse
{
    public long Id { get; set; }
    public long ProcurementId { get; set; }
    public int SortOrder { get; set; }

    // RFQ snapshot
    public long? SourceRfqId { get; set; }
    public long? SourceRfqItemId { get; set; }
    public string? RfqName { get; set; }
    public int? RfqExType { get; set; }
    public long? PartNumberId { get; set; }
    public string? PartNumberName { get; set; }
    public string? PartNumberDescription { get; set; }
    public double? RfqQty { get; set; }
    public string? RfqCondition { get; set; }
    public string? RfqUnit { get; set; }
    public string? RfqPriority { get; set; }
    public string? RfqAlt { get; set; }
    public string? RfqNote { get; set; }

    // Quote snapshot
    public long? SourceQuoteId { get; set; }
    public long? SourceQuoteItemId { get; set; }
    public string? QuoteNumber { get; set; }
    public decimal QuoteUnitPrice { get; set; }
    public int QuoteQty { get; set; }
    public string? QuoteCondition { get; set; }
    public string? QuoteAlt { get; set; }
    public int? QuoteLeadTimeDays { get; set; }

    // Supplier snapshot
    public long? SourceProcumentRecordId { get; set; }
    public long? SourceSupplierId { get; set; }
    public string? SupplierName { get; set; }
    public decimal? SupplierPrice { get; set; }
    public string? SupplierLeadTime { get; set; }
    public string? SupplierCondition { get; set; }
    public string? SupplierCertName { get; set; }
    public double? ShippingCost { get; set; }

    // Invoice acceptance snapshot
    public long SourceInvoiceItemId { get; set; }
    public int AcceptedQty { get; set; }
    public decimal AcceptedUnitPrice { get; set; }

    // Editable
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public long? CurrentSupplierId { get; set; }
    public string? CurrentSupplierName { get; set; }
    public string? LeadTime { get; set; }
    public string? Condition { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public string? Alt { get; set; }
    public string? Note { get; set; }
    public string ItemStatus { get; set; } = "Open";

    // Loop / recycle tracking
    public int LoopCount { get; set; }
    public string? LastReturnReason { get; set; }
    public DateTime? LastReturnedAt { get; set; }
    public long? FulfilledByPOItemId { get; set; }

    /// <summary>True when this item has at least one active (non-returned) POItem — i.e. it has been individually finalized.</summary>
    public bool HasActivePOItem { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public List<ProcurementSupplierQuoteResponse> SupplierQuotes { get; set; } = new();
}

public class ProcurementSupplierQuoteResponse
{
    public long Id { get; set; }
    public long ProcurementItemId { get; set; }
    public long? SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public double Qty { get; set; }
    public string? Condition { get; set; }
    public string? Unit { get; set; }
    public string? Alt { get; set; }
    public string? LeadTime { get; set; }
    public string? CertName { get; set; }
    public double? ShippingCost { get; set; }
    public string? Note { get; set; }
    public DateOnly? TagDate { get; set; }
    public string? ShippingPoint { get; set; }
    public bool IsSelected { get; set; }
    public long? SourceProcumentRecordId { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? AddedByUserId { get; set; }
    /// <summary>True when this selected supplier quote has an active (non-returned) POItem — i.e. admin has already approved this row.</summary>
    public bool HasActivePOItem { get; set; }
}

public class ProcurementAssignedUser
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? UserEmail { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Permission { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>Result of a Finalize call — the newly created POs (one per supplier).</summary>
public class FinalizeProcurementResponse
{
    public long ProcurementId { get; set; }
    public List<long> CreatedPOIds { get; set; } = new();
    public List<long> CreatedPOItemIds { get; set; } = new();
}

/// <summary>Flat view of a single ProcurementItem with parent procurement context, for the all-items list page.</summary>
public class ProcurementItemFlatResponse
{
    public long Id { get; set; }
    public long ProcurementId { get; set; }
    public string ProcurementStatus { get; set; } = "Open";
    public string? CustomerName { get; set; }

    public string? PartNumberName { get; set; }
    public string? PartNumberDescription { get; set; }

    public int Qty { get; set; }
    public string? Condition { get; set; }
    public string? Alt { get; set; }
    public string? Note { get; set; }
    public string ItemStatus { get; set; } = "Open";

    public string? CurrentSupplierName { get; set; }
    public decimal UnitPrice { get; set; }
    public string? LeadTime { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Users assigned to this specific item (EntityName=Procurement, EntityId=item.Id). Admin-only.</summary>
    public List<ProcurementAssignedUser> AssignedUsers { get; set; } = new();
}

/// <summary>Result of finalizing a single ProcurementItem (one supplier row).</summary>
public class FinalizeProcurementItemResponse
{
    public long ProcurementId { get; set; }
    public long ProcurementItemId { get; set; }
    /// <summary>POItem IDs created by this finalization (1 if single-supplier, 2+ if multi-supplier split).</summary>
    public List<long> CreatedPOItemIds { get; set; } = new();
    /// <summary>True when ALL items in the procurement are now materialized — procurement status set to Finalized.</summary>
    public bool ProcurementFullyFinalized { get; set; }
}
