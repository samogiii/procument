namespace Procument.Module.Sales.DTOs;

public class CustomerFollowUpResponse
{
    public long Id { get; set; }
    public string? CustomerCode { get; set; }
    public string? Category { get; set; }
    /// <summary>The linked RFQ; null when none is linked or it was deleted since.</summary>
    public CustomerFollowUpRfqRef? Rfq { get; set; }
    /// <summary>"Other Refs": purchase orders, quotes, proforma and final invoices.</summary>
    public List<CustomerFollowUpRefResponse> Refs { get; set; } = [];
    public string? PartNumber { get; set; }
    public string? RequestedItem { get; set; }
    public DateTime? DateOfRequest { get; set; }
    public DateTime? Deadline { get; set; }
    public string? Assign { get; set; }
    public string ProcurementStatus { get; set; } = "Open";
    public string SalesStatus { get; set; } = "Open";
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
    public List<string> Categories { get; set; } = [];
    public List<string> IssueStatuses { get; set; } = [];
    /// <summary>Open, In Progress, Done — for both Procurement and Sales status.</summary>
    public List<string> WorkStatuses { get; set; } = [];
    /// <summary>The current user is a procurement user (Menu Access "followUpProcurement"): rows with Procurement Status Done are hidden for them.</summary>
    public bool IsProcurementUser { get; set; }
}

public class CustomerFollowUpRfqRef
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Status { get; set; }
    public string? CustomerCode { get; set; }
}

public class CustomerFollowUpRefResponse
{
    /// <summary>PurchaseOrder | Quote | Invoice | FinalInvoice.</summary>
    public string Type { get; set; } = string.Empty;
    public long Id { get; set; }
    /// <summary>The document number; "#id" when the document no longer exists.</summary>
    public string Label { get; set; } = string.Empty;
    public bool Missing { get; set; }
}

/// <summary>One search hit in the RFQ / Other Refs pickers.</summary>
public class CustomerFollowUpRefOption
{
    public long Id { get; set; }
    public string Label { get; set; } = string.Empty;
    /// <summary>Customer / supplier, status, date — shown under the label.</summary>
    public string? Detail { get; set; }
    public string? Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CustomerFollowUpRefOptionsPage
{
    public List<CustomerFollowUpRefOption> Items { get; set; } = [];
    public bool HasMore { get; set; }
}

public class SetCustomerFollowUpRefsRequest
{
    public List<CustomerFollowUpRefKey> Refs { get; set; } = [];
}

public class CustomerFollowUpRefKey
{
    public string Type { get; set; } = string.Empty;
    public long Id { get; set; }
}

/// <summary>Links an existing RFQ to the row (null unlinks).</summary>
public class SetCustomerFollowUpRfqRequest
{
    public long? RfqId { get; set; }
}

/// <summary>Creates a new RFQ (through the bulk import, so part numbers are created on the fly) and links it to the row.</summary>
public class CreateCustomerFollowUpRfqRequest
{
    public string Name { get; set; } = string.Empty;
    public long CustomerId { get; set; }
    /// <summary>yyyy-MM-dd; becomes the RFQ's lead time.</summary>
    public string? Deadline { get; set; }
    public List<CreateCustomerFollowUpRfqItem> Items { get; set; } = [];
}

public class CreateCustomerFollowUpRfqItem
{
    public string PartNumber { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double Qty { get; set; } = 1;
}

public class CustomerFollowUpCustomerOption
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? CustomerCode { get; set; }
}
