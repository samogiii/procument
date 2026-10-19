namespace Procument.Module.Sales.DTOs;

public class CustomerFollowUpResponse
{
    public long Id { get; set; }
    public string? CustomerCode { get; set; }
    public string? PartNumber { get; set; }
    public string? RequestedItem { get; set; }
    public DateTime? DateOfRequest { get; set; }
    public DateTime? Deadline { get; set; }
    public string? Assign { get; set; }
    public string? ResponseStatus { get; set; }
    public string? IssueStatus { get; set; }
    public DateTime? DateOfReply { get; set; }
    public string? OurResponse { get; set; }
    public string? Feedback { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifyAt { get; set; }
    public int DocumentCount { get; set; }
    public int MessageCount { get; set; }
    /// <summary>Someone else wrote in the chat since the current user last opened it, or they marked it unread.</summary>
    public bool HasUnreadMessages { get; set; }
}

/// <summary>Chat state of one row for the current user — polled by the page so row colors follow new messages.</summary>
public class CustomerFollowUpMessageStatusResponse
{
    public long Id { get; set; }
    public int MessageCount { get; set; }
    public bool HasUnreadMessages { get; set; }
}

public class CustomerFollowUpMessageResponse
{
    public long Id { get; set; }
    public string? Text { get; set; }
    public string SentBy { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    /// <summary>The attached PDF, or null when there is none or it was deleted (then DocumentName still names it).</summary>
    public CustomerFollowUpDocumentResponse? Document { get; set; }
    public string? DocumentName { get; set; }
}

public class CustomerFollowUpDocumentResponse
{
    public long Id { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = "application/pdf";
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; }
    public string? UploadedBy { get; set; }
}

/// <summary>New blank row. DateOfRequest is the user's local "today" (yyyy-MM-dd); the server's UTC date is the fallback.</summary>
public class CreateCustomerFollowUpRequest
{
    public string? DateOfRequest { get; set; }
}

/// <summary>
/// One cell edit. Field is the column key (e.g. "issueStatus"); Value is the new text, or
/// yyyy-MM-dd for date columns. Empty / null clears the cell.
/// Saving one cell at a time keeps two users editing different cells of a row from overwriting each other.
/// </summary>
public class UpdateCustomerFollowUpFieldRequest
{
    public string Field { get; set; } = string.Empty;
    public string? Value { get; set; }
}

public class CustomerFollowUpOptionsResponse
{
    public List<string> Customers { get; set; } = [];
    public List<string> Assignees { get; set; } = [];
    public List<string> ResponseStatuses { get; set; } = [];
    public List<string> IssueStatuses { get; set; } = [];
}
