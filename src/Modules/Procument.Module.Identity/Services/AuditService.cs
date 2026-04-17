using Microsoft.EntityFrameworkCore;

using Procument.Shared.Entities;

namespace Procument.Module.Identity.Services;

public class AuditService : IAuditService
{
    private readonly DbContext _context;

    public AuditService(DbContext context)
    {
        _context = context;
    }

    public async Task LogAsync(long? userId, string action, string entityName, string entityId, string? details = null)
    {
        var log = new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Details = details,
            Timestamp = DateTime.UtcNow
        };

        _context.Set<AuditLog>().Add(log);
        await _context.SaveChangesAsync();
    }

    // Business logging methods with human-readable descriptions
    public async Task LogBusinessAsync(long? userId, string userName, string entityType, string entityId, string entityDisplayName,
                                       string actionCategory, string description, string? contextData = null, string? ipAddress = null,
                                       string? relatedEntityId = null, string? relatedEntityType = null)
    {
        var log = new AuditLog
        {
            UserId = userId,
            UserName = userName,
            Action = actionCategory,
            EntityName = entityType,
            EntityId = entityId,
            EntityDisplayName = entityDisplayName,
            ActionCategory = actionCategory,
            Details = description,
            ContextData = contextData,
            IPAddress = ipAddress,
            RelatedEntityId = relatedEntityId,
            RelatedEntityType = relatedEntityType,
            Timestamp = DateTime.UtcNow
        };

        _context.Set<AuditLog>().Add(log);
        await _context.SaveChangesAsync();
    }

    // RFQ Events
    public async Task LogRFQCreatedAsync(long? userId, string userName, long rfqId, string rfqName, string? customerName)
    {
        var displayName = $"RFQ #{rfqId}";
        var description = $"Created {displayName}{(!string.IsNullOrEmpty(customerName) ? $" for {customerName}" : "")}";
        await LogBusinessAsync(userId, userName, "RFQ", rfqId.ToString(), displayName, "Creation", description);
    }

    public async Task LogRFQStatusChangedAsync(long? userId, string userName, long rfqId, string rfqName, string oldStatus, string newStatus, string? reason = null)
    {
        var displayName = $"RFQ #{rfqId}";
        var reasonText = !string.IsNullOrEmpty(reason) ? $" Reason: {reason}" : "";
        var description = $"Changed {displayName} status from '{oldStatus}' to '{newStatus}'.{reasonText}";
        await LogBusinessAsync(userId, userName, "RFQ", rfqId.ToString(), displayName, "StatusChange", description);
    }

    public async Task LogRFQNoQuoteAsync(long? userId, string userName, long rfqId, string rfqName, string? reason)
    {
        var displayName = $"RFQ #{rfqId}";
        var description = $"Marked {displayName} as No Quote.{(!string.IsNullOrEmpty(reason) ? $" Reason: {reason}" : "")}";
        await LogBusinessAsync(userId, userName, "RFQ", rfqId.ToString(), displayName, "StatusChange", description);
    }

    public async Task LogRFQNoQuoteRejectedAsync(long? userId, string userName, long rfqId, string rfqName, string? reason, string? rejectionNote = null)
    {
        var displayName = $"RFQ #{rfqId}";
        var description = $"Rejected For No Quote {displayName}.{(!string.IsNullOrEmpty(reason) ? $" Reason: {reason}" : "")}{(!string.IsNullOrEmpty(rejectionNote) ? $" Note: {rejectionNote}" : "")}";
        await LogBusinessAsync(userId, userName, "RFQ", rfqId.ToString(), displayName, "StatusChange", description);
    }

    public async Task LogRFQItemAddedAsync(long? userId, string userName, long rfqId, string rfqName, string partNumber, double qty)
    {
        var displayName = $"RFQ #{rfqId}";
        var description = $"Added item '{partNumber}' (Qty: {qty}) to {displayName}";
        await LogBusinessAsync(userId, userName, "RFQ", rfqId.ToString(), displayName, "ItemChange", description);
    }

    public async Task LogRFQItemUpdatedAsync(long? userId, string userName, long rfqId, string rfqName, string partNumber, string? changes)
    {
        var displayName = $"RFQ #{rfqId}";
        var description = $"Updated item '{partNumber}' in {displayName}{(!string.IsNullOrEmpty(changes) ? $": {changes}" : "")}";
        await LogBusinessAsync(userId, userName, "RFQ", rfqId.ToString(), displayName, "ItemChange", description);
    }

    public async Task LogRFQItemDeletedAsync(long? userId, string userName, long rfqId, string rfqName, string partNumber)
    {
        var displayName = $"RFQ #{rfqId}";
        var description = $"Removed item '{partNumber}' from {displayName}";
        await LogBusinessAsync(userId, userName, "RFQ", rfqId.ToString(), displayName, "ItemChange", description);
    }

    // Quote Events
    public async Task LogQuoteCreatedAsync(long? userId, string userName, long quoteId, string quoteNumber, long rfqId, string rfqName)
    {
        var displayName = $"Quote #{quoteId}";
        var description = $"Created {displayName} from RFQ #{rfqId}";
        await LogBusinessAsync(userId, userName, "Quote", quoteId.ToString(), displayName, "Creation", description,
                              relatedEntityId: rfqId.ToString(), relatedEntityType: "RFQ");
    }

    public async Task LogQuoteStatusChangedAsync(long? userId, string userName, long quoteId, string quoteNumber, string oldStatus, string newStatus, string? reason = null)
    {
        var displayName = $"Quote #{quoteId}";
        var reasonText = !string.IsNullOrEmpty(reason) ? $" Reason: {reason}" : "";
        var description = $"Changed {displayName} status from '{oldStatus}' to '{newStatus}'.{reasonText}";
        await LogBusinessAsync(userId, userName, "Quote", quoteId.ToString(), displayName, "StatusChange", description);
    }

    public async Task LogQuoteEditedAsync(long? userId, string userName, long quoteId, string quoteNumber)
    {
        var displayName = $"Quote #{quoteId}";
        var description = $"Edited {displayName} (status reset to Draft)";
        await LogBusinessAsync(userId, userName, "Quote", quoteId.ToString(), displayName, "Update", description);
    }

    // Invoice Events
    public async Task LogInvoiceCreatedAsync(long? userId, string userName, long invoiceId, string invoiceNumber, long quoteId, string quoteNumber)
    {
        var displayName = $"Invoice #{invoiceId}";
        var description = $"Created {displayName} from Quote #{quoteId}";
        await LogBusinessAsync(userId, userName, "Invoice", invoiceId.ToString(), displayName, "Creation", description,
                              relatedEntityId: quoteId.ToString(), relatedEntityType: "Quote");
    }

    public async Task LogInvoiceStatusChangedAsync(long? userId, string userName, long invoiceId, string invoiceNumber, string oldStatus, string newStatus, string? reason = null)
    {
        var displayName = $"Invoice #{invoiceId}";
        var reasonText = !string.IsNullOrEmpty(reason) ? $" Reason: {reason}" : "";
        var description = $"Changed {displayName} status from '{oldStatus}' to '{newStatus}'.{reasonText}";
        await LogBusinessAsync(userId, userName, "Invoice", invoiceId.ToString(), displayName, "StatusChange", description);
    }

    // PO Events
    public async Task LogPOCreatedAsync(long? userId, string userName, long poId, string poNumber, long invoiceId, string invoiceNumber)
    {
        var displayName = $"PO #{poId}";
        var description = $"Created {displayName} from Invoice #{invoiceId}";
        await LogBusinessAsync(userId, userName, "PO", poId.ToString(), displayName, "Creation", description,
                              relatedEntityId: invoiceId.ToString(), relatedEntityType: "Invoice");
    }

    public async Task LogPOStatusChangedAsync(long? userId, string userName, long poId, string poNumber, string oldStatus, string newStatus)
    {
        var displayName = $"PO #{poId}";
        var description = $"Changed {displayName} status from '{oldStatus}' to '{newStatus}'";
        await LogBusinessAsync(userId, userName, "PO", poId.ToString(), displayName, "StatusChange", description);
    }

    public async Task<List<AuditLog>> GetLogsAsync(string entityName, string entityId)
    {
        return await _context.Set<AuditLog>()
            .Where(l => l.EntityName == entityName && l.EntityId == entityId)
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();
    }

    public async Task<List<AuditLog>> GetAllLogsAsync(int limit = 500)
    {
        return await _context.Set<AuditLog>()
            .OrderByDescending(l => l.Timestamp)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<AuditLog>> GetBusinessLogsAsync(string? entityType = null, string? entityId = null, string? actionCategory = null, int limit = 500)
    {
        var query = _context.Set<AuditLog>().AsQueryable();

        if (!string.IsNullOrEmpty(entityType))
            query = query.Where(l => l.EntityName == entityType);

        if (!string.IsNullOrEmpty(entityId))
            query = query.Where(l => l.EntityId == entityId);

        // Only apply actionCategory filter if it's provided
        // Note: This will only work after migration is applied
        // Before migration, ActionCategory will be null for all records
        if (!string.IsNullOrEmpty(actionCategory))
            query = query.Where(l => l.ActionCategory == actionCategory);

        return await query
            .OrderByDescending(l => l.Timestamp)
            .Take(limit)
            .ToListAsync();
    }
}
