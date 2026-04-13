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
    public long CustomerId { get; set; }
    public string? UserName { get; set; }
    public long? UserId { get; set; }
    public string? Notes { get; set; }
    public string? NoQuoteReason { get; set; }
    public int? ExType { get; set; }

    public bool IsUnread { get; set; }

    public List<RFQItemResponse> Items { get; set; } = new();
    public List<UserResponse> Views { get; set; } = new();
    public List<UserResponse> Edits { get; set; } = new();
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
