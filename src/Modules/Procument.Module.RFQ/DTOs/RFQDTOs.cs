namespace Procument.Module.RFQ.DTOs;

using Procument.Module.Identity.DTOs;
using Procument.Shared.Entities;

// ──── Request DTOs ────

public class CreateRFQRequest
{
    public string Name { get; set; } = string.Empty;
    public DateTime LeadTime { get; set; }
    public DateTime ReceivedDate { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public long UserId { get; set; }
    public string? Notes { get; set; }
    public int? ExType { get; set; }
    public List<string> PartNumbers { get; set; } = new();
}

public class UpdateRFQItemRequest
{
    public string? Alt { get; set; }
    public double Qty { get; set; }
    public string? Priority { get; set; }
    public string? Note { get; set; }
    public string? Condition { get; set; }
    public string? Unit { get; set; }
    public bool IsHighlighted { get; set; }
}

public class UpdateStatusRequest
{
    public string Status { get; set; } = string.Empty;
    public string? NoQuoteReason { get; set; }
}

public class RejectNoQuoteRequest
{
    public string? RejectionNote { get; set; }
}

public class UpdateExTypeRequest
{
    public int? ExType { get; set; }
}

public class UpdateRFQNotesRequest
{
    public string? Notes { get; set; }
}

public class UpdateRFQNameRequest
{
    public string Name { get; set; } = string.Empty;
}

public class UpdateRFQLeadTimeRequest
{
    public DateTime LeadTime { get; set; }
}

public class AddRFQItemRequest
{
    public string PartNumberName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double Qty { get; set; } = 1;
    public string? Condition { get; set; }
    public string? Priority { get; set; }
    public string? Note { get; set; }
    public string? Alt { get; set; }
    public string? Unit { get; set; }
    public List<string> Alternatives { get; set; } = new();
}

// ──── Response DTOs ────

/// <summary>Lightweight projection returned by the paginated list endpoint.</summary>
public class RFQListItem
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Status { get; set; } = "Open";
    public DateTime LeadTime { get; set; }
    public DateTime ReceivedDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CustomerName { get; set; } = "";
    public string? CustomerCode { get; set; }
    public long CustomerId { get; set; }
    public string? NoQuoteReason { get; set; }
    public string? RejectionNote { get; set; }
    public int? ExType { get; set; }
    public long UserId { get; set; }
    public string? UserName { get; set; }
    public bool IsUnread { get; set; }
    public int ItemCount { get; set; }
    public List<RFQListUserRef> Views { get; set; } = new();
    public List<RFQListUserRef> Edits { get; set; } = new();
}

public class RFQListUserRef
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string? AssignedAt { get; set; }
}

public class RFQResponse
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "Open";
    public DateTime LeadTime { get; set; }
    public DateTime ReceivedDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerCode { get; set; }
    public int? CustomerBase { get; set; }
    public string? CustomerCurrencyType { get; set; }
    /// <summary>Customer-specific quote coefficients — when set they take priority over the base/currency defaults.</summary>
    public decimal? CustomerCoef1 { get; set; }
    public decimal? CustomerCoef2 { get; set; }
    public decimal? CustomerCoef3 { get; set; }
    public string? CustomerTermsAndConditions { get; set; }
    public long CustomerId { get; set; }
    public string? UserName { get; set; }
    public long? UserId { get; set; }
    public string? Notes { get; set; }
    public string? NoQuoteReason { get; set; }
    public string? RejectionNote { get; set; }
    public int? ExType { get; set; }

    public bool IsUnread { get; set; }

    public List<RFQItemResponse> Items { get; set; } = new();
    public List<UserResponse> Views { get; set; } = new();
    public List<UserResponse> Edits { get; set; } = new();
}

public class RFQFlatItem
{
    public long ItemId { get; set; }
    public long RfqId { get; set; }
    public string RfqName { get; set; } = "";
    public string PartNumberName { get; set; } = "";
    public string? Description { get; set; }
    public double Qty { get; set; }
    public string? Condition { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerCode { get; set; }
    public string Status { get; set; } = "Open";
    public DateTime LeadTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<RFQListUserRef> AssignedUsers { get; set; } = new();
}

public class RFQItemResponse
{
    public long Id { get; set; }
    public string PartNumberName { get; set; } = string.Empty;
    public long PartNumberId { get; set; }
    public string? Alt { get; set; }
    public string? Description { get; set; }
    public double Qty { get; set; }
    public string? Condition { get; set; }
    public string? Priority { get; set; }
    public string? Remark { get; set; }
    public string? Note { get; set; }
    public string? Unit { get; set; }
    public bool IsHighlighted { get; set; }
    public List<AlternativeResponse> Alternatives { get; set; } = new();
}

public class AlternativeResponse
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

// ──── Bulk Import ────

/// <summary>
/// Payload for POST /api/rfqs/bulk-import — one customer, many RFQs, each with its own items.
/// The customer is addressed by its code, never by name.
/// </summary>
public class BulkImportRFQRequest
{
    /// <summary>Customer code (Customer.CustomerCode). Required — the customer must already exist.</summary>
    public string CustomerCode { get; set; } = string.Empty;
    /// <summary>Owner of the created RFQs. Falls back to the calling user when 0.</summary>
    public long UserId { get; set; }
    /// <summary>Ex-work type. Falls back to the customer's ExWork when null.</summary>
    public int? ExType { get; set; }
    public List<BulkImportRFQGroup> Rfqs { get; set; } = new();
}

public class BulkImportRFQGroup
{
    public string RfqName { get; set; } = string.Empty;
    public DateTime? ReceivedDate { get; set; }
    /// <summary>Maps to RFQHeader.LeadTime.</summary>
    public DateTime? Deadline { get; set; }
    public string? Notes { get; set; }
    public List<BulkImportRFQItem> Items { get; set; } = new();
}

public class BulkImportRFQItem
{
    public string PartNumber { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double Qty { get; set; } = 1;
    public string? Condition { get; set; }
    public string? Priority { get; set; }
    public string? Unit { get; set; }
    /// <summary>Stored on the RFQ item's Note and on the part number's Remark, like the per-row client flow.</summary>
    public string? Remark { get; set; }
    public List<string> Alternatives { get; set; } = new();
}

public class BulkImportRFQResponse
{
    /// <summary>The customer the import resolved to.</summary>
    public long CustomerId { get; set; }
    public string? CustomerCode { get; set; }
    public int CreatedCount { get; set; }
    public int SkippedCount { get; set; }
    public List<BulkImportRFQResult> Results { get; set; } = new();
}

public class BulkImportRFQResult
{
    /// <summary>Final name, which may carry a (1)/(2) suffix when one name was split by deadline.</summary>
    public string Name { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public long? RfqId { get; set; }
    public DateTime? Deadline { get; set; }
    public int ItemCount { get; set; }
    public bool Skipped { get; set; }
    public string? Reason { get; set; }
}
