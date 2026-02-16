namespace Procument.Module.RFQ.DTOs;

// ──── Request DTOs ────

public class CreateRFQRequest
{
    public string Name { get; set; } = string.Empty;
    public DateTime LeadTime { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public long UserId { get; set; }
    public List<string> PartNumbers { get; set; } = new();
}

public class UpdateRFQItemRequest
{
    public string? Alt { get; set; }
    public int Qty { get; set; }

    public string? Condition { get; set; }
}

// ──── Response DTOs ────

public class RFQResponse
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime LeadTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public long CustomerId { get; set; }
    public string? UserName { get; set; }
    public long? UserId { get; set; }
    public List<RFQItemResponse> Items { get; set; } = new();

}

public class RFQItemResponse
{
    public long Id { get; set; }
    public string PartNumberName { get; set; } = string.Empty;
    public long PartNumberId { get; set; }
    public string? Alt { get; set; }
    public string? Description { get; set; }
    public int Qty { get; set; }
    public string? Condition { get; set; }
}
