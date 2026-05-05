using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Procument.Shared.Entities;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Procument.Module.RFQ.Entities;

namespace Procument.API.Interceptors;

public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditSaveChangesInterceptor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (context == null) return await base.SavingChangesAsync(eventData, result, cancellationToken);

        // Update parent RFQ timestamp if item changes
        UpdateRfqTimestamps(context);

        // Snapshot changes *before* save
        var auditEntries = CreateAuditEntries(context);

        // Store them in HttpContext if any
        if (auditEntries.Count > 0 && _httpContextAccessor.HttpContext != null)
        {
            _httpContextAccessor.HttpContext.Items["AuditEntries"] = auditEntries;
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateRfqTimestamps(DbContext context)
    {
        var entries = context.ChangeTracker.Entries()
            .Where(e => e.Entity is RFQItem && (e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted))
            .ToList();

        foreach (var entry in entries)
        {
            var item = (RFQItem)entry.Entity;
            long rfqId = item.RFQId;

            if (rfqId == 0)
            {
                var rfqProp = entry.Property("RFQId");
                if (rfqProp.CurrentValue != null) rfqId = (long)rfqProp.CurrentValue;
            }

            if (rfqId > 0)
            {
                var rfq = context.Set<RFQHeader>().Local.FirstOrDefault(r => r.Id == rfqId)
                          ?? context.Set<RFQHeader>().FirstOrDefault(r => r.Id == rfqId);

                if (rfq != null)
                {
                    rfq.ModifyAt = DateTime.UtcNow;
                    context.Entry(rfq).State = EntityState.Modified;
                }
            }
        }
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (context == null || _httpContextAccessor.HttpContext == null)
            return await base.SavedChangesAsync(eventData, result, cancellationToken);

        // Check and Remove to prevent recursion/re-processing
        if (_httpContextAccessor.HttpContext.Items.TryGetValue("AuditEntries", out var entriesObj)
            && entriesObj is List<AuditEntry> auditEntries)
        {
            _httpContextAccessor.HttpContext.Items.Remove("AuditEntries");

            var logs = new List<AuditLog>();
            foreach (var entry in auditEntries)
            {
                // Update ID if it was temporary (Added entities)
                if (entry.Entry.State == EntityState.Added || entry.Entry.State == EntityState.Unchanged)
                {
                    // Try to find PK
                    var pk = entry.Entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey());
                    if (pk != null && pk.CurrentValue != null)
                    {
                        entry.EntityId = pk.CurrentValue.ToString() ?? entry.EntityId;
                    }
                }
                logs.Add(entry.ToAuditLog());
            }

            if (logs.Count > 0)
            {
                context.Set<AuditLog>().AddRange(logs);
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private List<AuditEntry> CreateAuditEntries(DbContext context)
    {
        context.ChangeTracker.DetectChanges();
        var entries = new List<AuditEntry>();

        var user = _httpContextAccessor.HttpContext?.User;
        long? userId = null;
        string userName = "System";

        if (user != null)
        {
            var idClaim = user.FindFirst(ClaimTypes.NameIdentifier) ?? user.FindFirst("sub");
            if (idClaim != null && long.TryParse(idClaim.Value, out var uid)) userId = uid;

            var nameClaim = user.FindFirst(ClaimTypes.Name) ?? user.FindFirst("name");
            if (nameClaim != null) userName = nameClaim.Value;
            else if (userId.HasValue) userName = $"User #{userId}";
        }

        foreach (var entry in context.ChangeTracker.Entries())
        {
            // Skip AuditLog itself to avoid loops
            if (entry.Entity is AuditLog || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                continue;

            var auditEntry = new AuditEntry(entry)
            {
                UserId = userId,
                UserName = userName,
                EntityName = entry.Entity.GetType().Name,
                Action = entry.State.ToString(),
                Timestamp = DateTime.UtcNow
            };

            // Automatic timestamping
            if (entry.Entity is AuditableEntity auditable)
            {
                if (entry.State == EntityState.Modified)
                {
                    auditable.ModifyAt = DateTime.UtcNow;
                }
            }

            foreach (var prop in entry.Properties)
            {
                if (prop.IsTemporary)
                {
                    // value will be generated by DB, skip for now
                    continue;
                }

                if (prop.Metadata.IsPrimaryKey())
                {
                    auditEntry.EntityId = prop.CurrentValue?.ToString() ?? "0";
                    continue;
                }

                string propertyName = prop.Metadata.Name;

                if (entry.State == EntityState.Added)
                {
                    auditEntry.NewValues[propertyName] = prop.CurrentValue;
                }
                else if (entry.State == EntityState.Deleted)
                {
                    auditEntry.OldValues[propertyName] = prop.OriginalValue;
                }
                else if (entry.State == EntityState.Modified)
                {
                    if (prop.IsModified)
                    {
                        auditEntry.ChangedColumns.Add(propertyName);
                        auditEntry.OldValues[propertyName] = prop.OriginalValue;
                        auditEntry.NewValues[propertyName] = prop.CurrentValue;
                    }
                }
            }
            entries.Add(auditEntry);
        }
        return entries;
    }
}

public class AuditEntry
{
    public EntityEntry Entry { get; }
    public long? UserId { get; set; }
    public string? UserName { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object?> OldValues { get; } = new();
    public Dictionary<string, object?> NewValues { get; } = new();
    public List<string> ChangedColumns { get; } = new();

    public AuditEntry(EntityEntry entry)
    {
        Entry = entry;
    }

    public AuditLog ToAuditLog()
    {
        var log = new AuditLog
        {
            UserId = UserId,
            UserName = UserName,
            EntityName = EntityName,
            EntityId = EntityId,
            Timestamp = Timestamp,
            Action = Action
        };

        if (OldValues.Count > 0) log.OldValues = JsonSerializer.Serialize(OldValues);
        if (NewValues.Count > 0) log.NewValues = JsonSerializer.Serialize(NewValues);
        if (ChangedColumns.Count > 0) log.AffectedColumns = JsonSerializer.Serialize(ChangedColumns);

        // Details
        if (Action == "Added")
        {
            log.Details = $"Created {EntityName}";
        }
        else if (Action == "Deleted")
        {
            log.Details = $"Deleted {EntityName}";
        }
        else if (Action == "Modified")
        {
            var changes = new List<string>();
            foreach (var col in ChangedColumns)
            {
                var oldV = OldValues.TryGetValue(col, out var ov) ? ov?.ToString() ?? "null" : "null";
                var newV = NewValues.TryGetValue(col, out var nv) ? nv?.ToString() ?? "null" : "null";

                // Truncate logic
                if (oldV.Length > 50) oldV = oldV[..47] + "...";
                if (newV.Length > 50) newV = newV[..47] + "...";

                changes.Add($"{col}: {oldV} -> {newV}");
            }
            log.Details = $"Updated {EntityName}: " + string.Join(", ", changes);
        }
        else
        {
            log.Details = $"{Action} {EntityName}";
        }

        return log;
    }
}
