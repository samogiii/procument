namespace Procument.Module.Identity.Services;

public interface IAuditService
{
    Task LogAsync(long? userId, string action, string entityName, string entityId, string? details = null);
    Task<List<Shared.Entities.AuditLog>> GetLogsAsync(string entityName, string entityId);
    Task<List<Shared.Entities.AuditLog>> GetAllLogsAsync(int limit = 100);
    Task<List<Shared.Entities.AuditLog>> GetBusinessLogsAsync(string? entityType = null, string? entityId = null, string? actionCategory = null, int limit = 100);

    // Business event logging methods
    Task LogRFQCreatedAsync(long? userId, string userName, long rfqId, string rfqName, string? customerName);
    Task LogRFQStatusChangedAsync(long? userId, string userName, long rfqId, string rfqName, string oldStatus, string newStatus, string? reason = null);
    Task LogRFQNoQuoteAsync(long? userId, string userName, long rfqId, string rfqName, string? reason);
    Task LogRFQNoQuoteRejectedAsync(long? userId, string userName, long rfqId, string rfqName, string? reason, string? rejectionNote = null);
    Task LogRFQItemAddedAsync(long? userId, string userName, long rfqId, string rfqName, string partNumber, double qty);
    Task LogRFQItemUpdatedAsync(long? userId, string userName, long rfqId, string rfqName, string partNumber, string? changes);
    Task LogRFQItemDeletedAsync(long? userId, string userName, long rfqId, string rfqName, string partNumber);
    Task LogQuoteCreatedAsync(long? userId, string userName, long quoteId, string quoteNumber, long rfqId, string rfqName);
    Task LogQuoteStatusChangedAsync(long? userId, string userName, long quoteId, string quoteNumber, string oldStatus, string newStatus, string? reason = null);
    Task LogQuoteEditedAsync(long? userId, string userName, long quoteId, string quoteNumber);
    Task LogInvoiceCreatedAsync(long? userId, string userName, long invoiceId, string invoiceNumber, long quoteId, string quoteNumber);
    Task LogInvoiceStatusChangedAsync(long? userId, string userName, long invoiceId, string invoiceNumber, string oldStatus, string newStatus, string? reason = null);
    Task LogPOCreatedAsync(long? userId, string userName, long poId, string poNumber, long invoiceId, string invoiceNumber);
    Task LogPOStatusChangedAsync(long? userId, string userName, long poId, string poNumber, string oldStatus, string newStatus);
}
