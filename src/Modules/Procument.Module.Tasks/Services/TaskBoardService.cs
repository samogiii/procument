using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Identity.Entities;
using Procument.Module.Purchasing.Entities;
using Procument.Module.RFQ.Entities;
using Procument.Module.Sales.DTOs;
using Procument.Module.Sales.Entities;
using Procument.Module.Sales.Services;
using Procument.Module.Tasks.DTOs;
using Procument.Module.Tasks.Entities;

namespace Procument.Module.Tasks.Services;

/// <summary>Who is calling. SuperAdmin may open and change every user's board; everyone else only their own.</summary>
public record TaskBoardActor(long UserId, string UserName, bool IsSuperAdmin)
{
    public bool CanAccess(long ownerUserId) => IsSuperAdmin || ownerUserId == UserId;
}

/// <summary>The caller may not touch that board.</summary>
public class TaskBoardForbiddenException : Exception;

public interface ITaskBoardService
{
    Task<TaskBoardResponse> GetBoardAsync(TaskBoardActor actor, long? userId);
    Task<List<TaskBoardUserResponse>> GetUsersAsync();
    Task<int> GetPendingCountAsync(long userId);
    Task<CustomerFollowUpRefOptionsPage> SearchRefsAsync(string type, string? search, int skip, int take);

    Task<TaskColumnResponse> CreateColumnAsync(TaskBoardActor actor, CreateTaskColumnRequest request);
    Task<TaskColumnResponse> UpdateColumnAsync(TaskBoardActor actor, long id, UpdateTaskColumnRequest request);
    Task DeleteColumnAsync(TaskBoardActor actor, long id);
    Task ReorderColumnsAsync(TaskBoardActor actor, ReorderTaskColumnsRequest request);

    Task<TaskCardResponse> CreateCardAsync(TaskBoardActor actor, CreateTaskCardRequest request);
    Task<TaskCardDetailResponse> GetCardAsync(TaskBoardActor actor, long id);
    Task<TaskCardDetailResponse> UpdateCardFieldAsync(TaskBoardActor actor, long id, UpdateTaskCardFieldRequest request);
    Task MoveCardAsync(TaskBoardActor actor, long id, MoveTaskCardRequest request);
    Task<TaskCardDetailResponse> SetTagsAsync(TaskBoardActor actor, long id, List<string> names);
    Task<TaskCardDetailResponse> SetRefsAsync(TaskBoardActor actor, long id, List<TaskRefKey> refs);
    Task<TaskCardDetailResponse> AddCommentAsync(TaskBoardActor actor, long id, string text);
    Task<TaskCardDetailResponse> DeleteCommentAsync(TaskBoardActor actor, long id, long activityId);
    Task<List<TaskTagResponse>> DeleteCardAsync(TaskBoardActor actor, long id);
}

public class TaskBoardService : ITaskBoardService
{
    private const int MaxTitleLength = 500;
    private const int MaxTextLength = 4000;
    private const int MaxTagLength = 60;
    private const int MaxColumnNameLength = 100;

    private static readonly (string Name, string Color, bool IsDone)[] DefaultColumns =
    [
        ("To Do", "#6E7681", false),
        ("Planning", "#8B949E", false),
        ("In Progress", "#5B4FD8", false),
        ("Waiting for Supplier", "#D4651F", false),
        ("Done", "#E3B341", true),
        ("On Hold", "#8D6E63", false),
    ];

    private static readonly string[] TagPalette =
        ["#1565C0", "#6A1B9A", "#2E7D32", "#C62828", "#EF6C00", "#00838F", "#AD1457", "#5D4037", "#283593", "#558B2F"];

    private readonly DbContext _db;
    private readonly ICustomerFollowUpService _followUps;

    public TaskBoardService(DbContext db, ICustomerFollowUpService followUps)
    {
        _db = db;
        _followUps = followUps;
    }

    private static DateTime ServerToday => DateTime.Now.Date;

    private static string? DateText(DateTime? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    // ── Board ────────────────────────────────────────────────────────────────

    public async Task<TaskBoardResponse> GetBoardAsync(TaskBoardActor actor, long? userId)
    {
        var ownerId = userId ?? actor.UserId;
        if (!actor.CanAccess(ownerId)) throw new TaskBoardForbiddenException();

        var owner = await _db.Set<User>().AsNoTracking().FirstOrDefaultAsync(u => u.Id == ownerId)
            ?? throw new KeyNotFoundException();
        await EnsureBoardAsync(owner);

        var columns = await _db.Set<TaskBoardColumn>().AsNoTracking()
            .Where(c => c.OwnerUserId == ownerId)
            .OrderBy(c => c.Position).ThenBy(c => c.Id)
            .ToListAsync();
        var cards = await LoadCardsAsync(_db.Set<TaskCard>().Where(c => c.OwnerUserId == ownerId));
        var byColumn = cards.ToLookup(c => c.ColumnId);

        return new TaskBoardResponse
        {
            OwnerUserId = ownerId,
            OwnerName = owner.Name,
            ServerToday = DateText(ServerToday)!,
            IsOtherUser = ownerId != actor.UserId,
            Columns = columns.Select(c => ColumnResponse(c, byColumn[c.Id].ToList())).ToList(),
            Tags = await TagsAsync(ownerId),
        };
    }

    public async Task<List<TaskBoardUserResponse>> GetUsersAsync()
    {
        var counts = await _db.Set<TaskCard>().AsNoTracking()
            .GroupBy(c => c.OwnerUserId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
        var users = await _db.Set<User>().AsNoTracking()
            .Where(u => u.IsActive)
            .OrderBy(u => u.Name)
            .Select(u => new { u.Id, u.Name, u.Role })
            .ToListAsync();
        return users.Select(u => new TaskBoardUserResponse
        {
            Id = u.Id, Name = u.Name, Role = u.Role, CardCount = counts.GetValueOrDefault(u.Id),
        }).ToList();
    }

    /// <summary>Cards of the user that are due today or overdue and not in a Done column.</summary>
    public Task<int> GetPendingCountAsync(long userId)
    {
        var today = ServerToday;
        return _db.Set<TaskCard>().AsNoTracking()
            .CountAsync(c => c.OwnerUserId == userId && !c.Column.IsDone && c.DueDate != null && c.DueDate <= today);
    }

    public Task<CustomerFollowUpRefOptionsPage> SearchRefsAsync(string type, string? search, int skip, int take) =>
        _followUps.SearchRefsAsync(TaskRefTypes.Normalize(type) ?? throw new ArgumentException($"Unknown reference type '{type}'."), search, skip, take);

    /// <summary>
    /// First opening of a board: creates the default columns and moves the user's tasks of the old
    /// Task Manager onto it (Not Started → To Do, In Progress / Review → In Progress, Done → Done).
    /// </summary>
    private async Task EnsureBoardAsync(User owner)
    {
        if (await _db.Set<TaskBoardColumn>().AnyAsync(c => c.OwnerUserId == owner.Id)) return;

        var columns = DefaultColumns.Select((c, i) => new TaskBoardColumn
        {
            OwnerUserId = owner.Id, Name = c.Name, Color = c.Color, IsDone = c.IsDone, Position = i,
        }).ToList();
        _db.Set<TaskBoardColumn>().AddRange(columns);

        var oldTasks = await _db.Set<TaskItem>().AsNoTracking()
            .Where(t => t.IsActive && (t.AssignedToUserId == owner.Id || t.AssignedTo == owner.Name))
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();
        var positions = new Dictionary<TaskBoardColumn, int>();
        foreach (var task in oldTasks)
        {
            var column = (int)task.Status switch
            {
                0 => columns[0],
                3 => columns[4],
                _ => columns[2],
            };
            var position = positions.GetValueOrDefault(column);
            positions[column] = position + 1;
            var card = new TaskCard
            {
                OwnerUserId = owner.Id,
                Column = column,
                Position = position,
                Title = Truncate(task.Title, MaxTitleLength),
                Description = task.Description,
                CreatedBy = task.CreatedByCode,
                CreatedAt = task.CreatedAt,
            };
            card.Activities.Add(new TaskCardActivity
            {
                Kind = TaskActivityKinds.Created,
                Text = $"Imported from the old Task Manager (created by {task.CreatedByCode})",
                UserName = task.CreatedByCode,
                CreatedAt = task.CreatedAt,
            });
            _db.Set<TaskCard>().Add(card);
        }

        await _db.SaveChangesAsync();
    }

    // ── Columns ──────────────────────────────────────────────────────────────

    public async Task<TaskColumnResponse> CreateColumnAsync(TaskBoardActor actor, CreateTaskColumnRequest request)
    {
        var ownerId = request.UserId ?? actor.UserId;
        if (!actor.CanAccess(ownerId)) throw new TaskBoardForbiddenException();
        var name = ColumnName(request.Name);

        var position = await _db.Set<TaskBoardColumn>().Where(c => c.OwnerUserId == ownerId)
            .Select(c => (int?)c.Position).MaxAsync() ?? -1;
        var column = new TaskBoardColumn
        {
            OwnerUserId = ownerId,
            Name = name,
            Color = ColorOrDefault(request.Color, "#6E7681"),
            Position = position + 1,
        };
        _db.Set<TaskBoardColumn>().Add(column);
        await _db.SaveChangesAsync();
        return ColumnResponse(column, []);
    }

    public async Task<TaskColumnResponse> UpdateColumnAsync(TaskBoardActor actor, long id, UpdateTaskColumnRequest request)
    {
        var column = await FindColumnAsync(actor, id);
        if (request.Name != null) column.Name = ColumnName(request.Name);
        if (request.Color != null) column.Color = ColorOrDefault(request.Color, column.Color);
        if (request.IsDone.HasValue) column.IsDone = request.IsDone.Value;
        await _db.SaveChangesAsync();
        return ColumnResponse(column, []);
    }

    public async Task DeleteColumnAsync(TaskBoardActor actor, long id)
    {
        var column = await FindColumnAsync(actor, id);
        if (await _db.Set<TaskCard>().AnyAsync(c => c.ColumnId == id))
            throw new ArgumentException($"\"{column.Name}\" still has cards — move or delete them first.");
        if (!await _db.Set<TaskBoardColumn>().AnyAsync(c => c.OwnerUserId == column.OwnerUserId && c.Id != id))
            throw new ArgumentException("A board needs at least one column.");
        _db.Set<TaskBoardColumn>().Remove(column);
        await _db.SaveChangesAsync();
    }

    public async Task ReorderColumnsAsync(TaskBoardActor actor, ReorderTaskColumnsRequest request)
    {
        var ownerId = request.UserId ?? actor.UserId;
        if (!actor.CanAccess(ownerId)) throw new TaskBoardForbiddenException();
        var columns = await _db.Set<TaskBoardColumn>().Where(c => c.OwnerUserId == ownerId).ToListAsync();
        if (columns.Count != request.Ids.Count || columns.Any(c => !request.Ids.Contains(c.Id)))
            throw new ArgumentException("The column list is out of date — reload the board.");
        foreach (var column in columns) column.Position = request.Ids.IndexOf(column.Id);
        await _db.SaveChangesAsync();
    }

    private async Task<TaskBoardColumn> FindColumnAsync(TaskBoardActor actor, long id)
    {
        var column = await _db.Set<TaskBoardColumn>().FirstOrDefaultAsync(c => c.Id == id) ?? throw new KeyNotFoundException();
        if (!actor.CanAccess(column.OwnerUserId)) throw new TaskBoardForbiddenException();
        return column;
    }

    private static string ColumnName(string? value)
    {
        var name = value?.Trim();
        if (string.IsNullOrEmpty(name)) throw new ArgumentException("Column name is required.");
        return Truncate(name, MaxColumnNameLength);
    }

    private static string ColorOrDefault(string? value, string fallback)
    {
        var color = value?.Trim();
        return !string.IsNullOrEmpty(color) && color.Length <= 20 ? color : fallback;
    }

    private static TaskColumnResponse ColumnResponse(TaskBoardColumn column, List<TaskCardResponse> cards) => new()
    {
        Id = column.Id,
        Name = column.Name,
        Color = column.Color,
        Position = column.Position,
        IsDone = column.IsDone,
        Cards = cards,
    };

    // ── Cards ────────────────────────────────────────────────────────────────

    public async Task<TaskCardResponse> CreateCardAsync(TaskBoardActor actor, CreateTaskCardRequest request)
    {
        var column = await FindColumnAsync(actor, request.ColumnId);
        var title = CardTitle(request.Title);
        var priority = request.Priority == null ? TaskPriorities.Normal
            : TaskPriorities.Normalize(request.Priority) ?? throw new ArgumentException($"Unknown priority '{request.Priority}'.");
        var dueDate = ParseDate(request.DueDate);

        int position;
        if (request.Top)
        {
            await _db.Set<TaskCard>().Where(c => c.ColumnId == column.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.Position, c => c.Position + 1));
            position = 0;
        }
        else
        {
            position = (await _db.Set<TaskCard>().Where(c => c.ColumnId == column.Id)
                .Select(c => (int?)c.Position).MaxAsync() ?? -1) + 1;
        }

        var card = new TaskCard
        {
            OwnerUserId = column.OwnerUserId,
            ColumnId = column.Id,
            Position = position,
            Title = title,
            Priority = priority,
            DueDate = dueDate,
            CreatedBy = actor.UserName,
        };
        card.Activities.Add(Activity(actor, TaskActivityKinds.Created, $"Created in {column.Name}"));
        _db.Set<TaskCard>().Add(card);
        await _db.SaveChangesAsync();

        return (await LoadCardsAsync(_db.Set<TaskCard>().Where(c => c.Id == card.Id)))[0];
    }

    public async Task<TaskCardDetailResponse> GetCardAsync(TaskBoardActor actor, long id)
    {
        await FindCardAsync(actor, id);
        return await DetailAsync(id);
    }

    public async Task<TaskCardDetailResponse> UpdateCardFieldAsync(TaskBoardActor actor, long id, UpdateTaskCardFieldRequest request)
    {
        var card = await FindCardAsync(actor, id);
        var value = request.Value?.Trim();
        string? log;
        string kind;

        switch (request.Field?.Trim().ToLowerInvariant())
        {
            case "title":
                var title = CardTitle(value);
                kind = TaskActivityKinds.Title;
                log = title == card.Title ? null : $"Renamed from \"{card.Title}\" to \"{title}\"";
                card.Title = title;
                break;
            case "description":
                var description = string.IsNullOrEmpty(value) ? null : request.Value;
                kind = TaskActivityKinds.Description;
                log = description == card.Description ? null : description == null ? "Cleared the description" : "Updated the description";
                card.Description = description;
                break;
            case "priority":
                var priority = TaskPriorities.Normalize(value) ?? throw new ArgumentException($"Unknown priority '{value}'.");
                kind = TaskActivityKinds.Priority;
                log = priority == card.Priority ? null : $"Priority {card.Priority} → {priority}";
                card.Priority = priority;
                break;
            case "duedate":
                var due = ParseDate(value);
                kind = TaskActivityKinds.DueDate;
                log = due == card.DueDate ? null
                    : due == null ? $"Removed the due date ({DateText(card.DueDate)})"
                    : card.DueDate == null ? $"Due date set to {DateText(due)}"
                    : $"Due date moved from {DateText(card.DueDate)} to {DateText(due)}";
                card.DueDate = due;
                break;
            default:
                throw new ArgumentException($"Unknown field '{request.Field}'.");
        }

        if (log != null)
        {
            card.UpdatedAt = DateTime.UtcNow;
            _db.Set<TaskCardActivity>().Add(Activity(actor, kind, log, card.Id));
            await _db.SaveChangesAsync();
        }
        return await DetailAsync(id);
    }

    /// <summary>Puts the card at <paramref name="request"/>.Index of the target column and renumbers both columns.</summary>
    public async Task MoveCardAsync(TaskBoardActor actor, long id, MoveTaskCardRequest request)
    {
        var card = await FindCardAsync(actor, id);
        var source = await _db.Set<TaskBoardColumn>().FirstAsync(c => c.Id == card.ColumnId);
        var target = source;
        if (request.ColumnId != card.ColumnId)
        {
            target = await _db.Set<TaskBoardColumn>().FirstOrDefaultAsync(c => c.Id == request.ColumnId) ?? throw new KeyNotFoundException();
            if (target.OwnerUserId != card.OwnerUserId) throw new ArgumentException("Cards can only move inside one board.");
        }

        var targetCards = await _db.Set<TaskCard>()
            .Where(c => c.ColumnId == target.Id && c.Id != id)
            .OrderBy(c => c.Position).ThenBy(c => c.Id)
            .ToListAsync();
        targetCards.Insert(Math.Clamp(request.Index, 0, targetCards.Count), card);
        for (var i = 0; i < targetCards.Count; i++) targetCards[i].Position = i;

        if (target != source)
        {
            var sourceCards = await _db.Set<TaskCard>()
                .Where(c => c.ColumnId == source.Id && c.Id != id)
                .OrderBy(c => c.Position).ThenBy(c => c.Id)
                .ToListAsync();
            for (var i = 0; i < sourceCards.Count; i++) sourceCards[i].Position = i;

            card.ColumnId = target.Id;
            card.UpdatedAt = DateTime.UtcNow;
            _db.Set<TaskCardActivity>().Add(Activity(actor, TaskActivityKinds.Moved, $"Moved from {source.Name} to {target.Name}", card.Id));
        }

        await _db.SaveChangesAsync();
    }

    /// <summary>Replaces the card's tags. Unknown names become new tags; tags no card uses any more are deleted.</summary>
    public async Task<TaskCardDetailResponse> SetTagsAsync(TaskBoardActor actor, long id, List<string> names)
    {
        var card = await FindCardAsync(actor, id);
        var wanted = names
            .Select(n => Truncate(string.Join(' ', (n ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries)), MaxTagLength))
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var ownerTags = await _db.Set<TaskTag>().Where(t => t.OwnerUserId == card.OwnerUserId).ToListAsync();
        var links = await _db.Set<TaskCardTag>().Include(l => l.Tag).Where(l => l.CardId == id).ToListAsync();

        var removed = links.Where(l => !wanted.Contains(l.Tag.Name, StringComparer.OrdinalIgnoreCase)).ToList();
        _db.Set<TaskCardTag>().RemoveRange(removed);

        var added = new List<string>();
        foreach (var name in wanted)
        {
            if (links.Any(l => string.Equals(l.Tag.Name, name, StringComparison.OrdinalIgnoreCase))) continue;
            var tag = ownerTags.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
            if (tag == null)
            {
                tag = new TaskTag { OwnerUserId = card.OwnerUserId, Name = name, Color = TagColor(name) };
                ownerTags.Add(tag);
                _db.Set<TaskTag>().Add(tag);
            }
            _db.Set<TaskCardTag>().Add(new TaskCardTag { CardId = id, Tag = tag });
            added.Add(tag.Name);
        }

        if (added.Count > 0 || removed.Count > 0)
        {
            var parts = added.Select(n => $"+{n}").Concat(removed.Select(l => $"−{l.Tag.Name}"));
            _db.Set<TaskCardActivity>().Add(Activity(actor, TaskActivityKinds.Tag, $"Tags {string.Join(", ", parts)}", id));
            card.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await DeleteUnusedTagsAsync(card.OwnerUserId);
        }
        return await DetailAsync(id);
    }

    /// <summary>Replaces the card's References (RFQs, quotes, invoices, purchase orders, final invoices).</summary>
    public async Task<TaskCardDetailResponse> SetRefsAsync(TaskBoardActor actor, long id, List<TaskRefKey> refs)
    {
        var card = await FindCardAsync(actor, id);
        var wanted = new HashSet<(string Type, long Id)>();
        foreach (var key in refs)
        {
            var type = TaskRefTypes.Normalize(key.Type) ?? throw new ArgumentException($"Unknown reference type '{key.Type}'.");
            wanted.Add((type, key.Id));
        }

        var labels = new Dictionary<(string, long), string>();
        foreach (var group in wanted.GroupBy(w => w.Type))
        {
            var found = await RefLabelsAsync(group.Key, group.Select(g => g.Id).ToList());
            var missing = group.FirstOrDefault(g => !found.ContainsKey(g.Id));
            if (missing != default) throw new ArgumentException($"{TaskRefTypes.Short(group.Key)} #{missing.Id} does not exist.");
            foreach (var (refId, label) in found) labels[(group.Key, refId)] = label;
        }

        var current = await _db.Set<TaskCardRef>().Where(r => r.CardId == id).ToListAsync();
        var removed = current.Where(r => !wanted.Contains((r.RefType, r.RefId))).ToList();
        var added = wanted.Where(w => !current.Any(r => r.RefType == w.Type && r.RefId == w.Id)).ToList();
        if (removed.Count == 0 && added.Count == 0) return await DetailAsync(id);

        _db.Set<TaskCardRef>().RemoveRange(removed);
        _db.Set<TaskCardRef>().AddRange(added.Select(a => new TaskCardRef { CardId = id, RefType = a.Type, RefId = a.Id }));

        var removedLabels = new List<string>();
        foreach (var group in removed.GroupBy(r => r.RefType))
        {
            var found = await RefLabelsAsync(group.Key, group.Select(r => r.RefId).ToList());
            removedLabels.AddRange(group.Select(r => $"−{TaskRefTypes.Short(r.RefType)} {found.GetValueOrDefault(r.RefId) ?? $"#{r.RefId}"}"));
        }
        var parts = added.Select(a => $"+{TaskRefTypes.Short(a.Type)} {labels[(a.Type, a.Id)]}").Concat(removedLabels);
        _db.Set<TaskCardActivity>().Add(Activity(actor, TaskActivityKinds.Ref, $"References {string.Join(", ", parts)}", id));
        card.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return await DetailAsync(id);
    }

    public async Task<TaskCardDetailResponse> AddCommentAsync(TaskBoardActor actor, long id, string text)
    {
        var card = await FindCardAsync(actor, id);
        var value = text?.Trim();
        if (string.IsNullOrEmpty(value)) throw new ArgumentException("Write a message first.");
        if (value.Length > MaxTextLength) throw new ArgumentException($"A message can be at most {MaxTextLength} characters.");
        _db.Set<TaskCardActivity>().Add(Activity(actor, TaskActivityKinds.Comment, value, card.Id));
        await _db.SaveChangesAsync();
        return await DetailAsync(id);
    }

    /// <summary>Only the author may delete a message; changes in the timeline cannot be deleted.</summary>
    public async Task<TaskCardDetailResponse> DeleteCommentAsync(TaskBoardActor actor, long id, long activityId)
    {
        await FindCardAsync(actor, id);
        var activity = await _db.Set<TaskCardActivity>().FirstOrDefaultAsync(a => a.Id == activityId && a.CardId == id && a.Kind == TaskActivityKinds.Comment)
            ?? throw new KeyNotFoundException();
        if (!string.Equals(activity.UserName, actor.UserName, StringComparison.OrdinalIgnoreCase)) throw new TaskBoardForbiddenException();
        _db.Set<TaskCardActivity>().Remove(activity);
        await _db.SaveChangesAsync();
        return await DetailAsync(id);
    }

    /// <summary>Deletes the card with its timeline, tags links and refs; returns the board's tags afterwards.</summary>
    public async Task<List<TaskTagResponse>> DeleteCardAsync(TaskBoardActor actor, long id)
    {
        var card = await FindCardAsync(actor, id);
        _db.Set<TaskCard>().Remove(card);
        await _db.SaveChangesAsync();
        await DeleteUnusedTagsAsync(card.OwnerUserId);
        return await TagsAsync(card.OwnerUserId);
    }

    private async Task<TaskCard> FindCardAsync(TaskBoardActor actor, long id)
    {
        var card = await _db.Set<TaskCard>().FirstOrDefaultAsync(c => c.Id == id) ?? throw new KeyNotFoundException();
        if (!actor.CanAccess(card.OwnerUserId)) throw new TaskBoardForbiddenException();
        return card;
    }

    private static string CardTitle(string? value)
    {
        var title = value?.Trim();
        if (string.IsNullOrEmpty(title)) throw new ArgumentException("Title is required.");
        return Truncate(title, MaxTitleLength);
    }

    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateTime.TryParseExact(value.Trim()[..Math.Min(10, value.Trim().Length)], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date.Date;
        throw new ArgumentException($"'{value}' is not a date (yyyy-MM-dd).");
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    private static TaskCardActivity Activity(TaskBoardActor actor, string kind, string text, long cardId = 0) => new()
    {
        CardId = cardId,
        Kind = kind,
        Text = Truncate(text, MaxTextLength),
        UserName = actor.UserName,
    };

    // ── Tags ─────────────────────────────────────────────────────────────────

    /// <summary>Same name → same color, so a re-created tag looks like before.</summary>
    private static string TagColor(string name)
    {
        var hash = 0;
        foreach (var ch in name.ToLowerInvariant()) hash = unchecked(hash * 31 + ch);
        return TagPalette[(hash & int.MaxValue) % TagPalette.Length];
    }

    private Task DeleteUnusedTagsAsync(long ownerId) =>
        _db.Set<TaskTag>().Where(t => t.OwnerUserId == ownerId && !t.Cards.Any()).ExecuteDeleteAsync();

    private Task<List<TaskTagResponse>> TagsAsync(long ownerId) =>
        _db.Set<TaskTag>().AsNoTracking()
            .Where(t => t.OwnerUserId == ownerId)
            .OrderBy(t => t.Name)
            .Select(t => new TaskTagResponse { Id = t.Id, Name = t.Name, Color = t.Color, Usage = t.Cards.Count })
            .ToListAsync();

    // ── Read models ──────────────────────────────────────────────────────────

    private async Task<TaskCardDetailResponse> DetailAsync(long id)
    {
        var card = (await LoadCardsAsync(_db.Set<TaskCard>().Where(c => c.Id == id))).FirstOrDefault() ?? throw new KeyNotFoundException();
        var extra = await _db.Set<TaskCard>().AsNoTracking().Where(c => c.Id == id)
            .Select(c => new { c.Description, c.OwnerUserId })
            .FirstAsync();
        var activities = await _db.Set<TaskCardActivity>().AsNoTracking()
            .Where(a => a.CardId == id)
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .Select(a => new TaskActivityResponse { Id = a.Id, Kind = a.Kind, Text = a.Text, UserName = a.UserName, CreatedAt = a.CreatedAt })
            .ToListAsync();
        foreach (var a in activities) a.CreatedAt = Utc(a.CreatedAt);

        return new TaskCardDetailResponse
        {
            Card = card,
            Description = extra.Description,
            Activities = activities,
            Tags = await TagsAsync(extra.OwnerUserId),
        };
    }

    /// <summary>Board-card view of the given cards, in column order.</summary>
    private async Task<List<TaskCardResponse>> LoadCardsAsync(IQueryable<TaskCard> query)
    {
        var rows = await query.AsNoTracking()
            .OrderBy(c => c.Position).ThenBy(c => c.Id)
            .Select(c => new
            {
                c.Id, c.ColumnId, c.Position, c.Title, c.Priority, c.DueDate, c.CreatedBy, c.CreatedAt,
                HasDescription = c.Description != null && c.Description != "",
                TagIds = c.Tags.Select(t => t.TagId).ToList(),
                CommentCount = c.Activities.Count(a => a.Kind == TaskActivityKinds.Comment),
                LastComment = c.Activities
                    .Where(a => a.Kind == TaskActivityKinds.Comment)
                    .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
                    .Select(a => new TaskLastComment { Text = a.Text, UserName = a.UserName, CreatedAt = a.CreatedAt })
                    .FirstOrDefault(),
            })
            .AsSplitQuery()
            .ToListAsync();
        if (rows.Count == 0) return [];

        var refs = await CardRefsAsync(rows.Select(r => r.Id).ToList());
        return rows.Select(r =>
        {
            if (r.LastComment != null) r.LastComment.CreatedAt = Utc(r.LastComment.CreatedAt);
            return new TaskCardResponse
            {
                Id = r.Id,
                ColumnId = r.ColumnId,
                Position = r.Position,
                Title = r.Title,
                Priority = r.Priority,
                DueDate = DateText(r.DueDate),
                HasDescription = r.HasDescription,
                TagIds = r.TagIds,
                Refs = refs.GetValueOrDefault(r.Id) ?? [],
                LastComment = r.LastComment,
                CommentCount = r.CommentCount,
                CreatedBy = r.CreatedBy,
                CreatedAt = Utc(r.CreatedAt),
            };
        }).ToList();
    }

    private async Task<Dictionary<long, List<TaskRefResponse>>> CardRefsAsync(List<long> cardIds)
    {
        var refs = await _db.Set<TaskCardRef>().AsNoTracking()
            .Where(r => cardIds.Contains(r.CardId))
            .OrderBy(r => r.Id)
            .ToListAsync();
        if (refs.Count == 0) return [];

        var labels = new Dictionary<string, Dictionary<long, string>>();
        foreach (var group in refs.GroupBy(r => r.RefType))
            labels[group.Key] = await RefLabelsAsync(group.Key, group.Select(r => r.RefId).Distinct().ToList());

        return refs
            .GroupBy(r => r.CardId)
            .ToDictionary(g => g.Key, g => g
                .OrderBy(r => Array.IndexOf(TaskRefTypes.All, r.RefType))
                .Select(r =>
                {
                    var label = labels.GetValueOrDefault(r.RefType)?.GetValueOrDefault(r.RefId);
                    return new TaskRefResponse { Type = r.RefType, Id = r.RefId, Label = label ?? $"#{r.RefId}", Missing = label == null };
                })
                .ToList());
    }

    /// <summary>Document numbers of one reference type, by id; ids that no longer exist are absent.</summary>
    private async Task<Dictionary<long, string>> RefLabelsAsync(string type, List<long> ids)
    {
        if (ids.Count == 0) return [];
        return type switch
        {
            TaskRefTypes.Rfq => await _db.Set<RFQHeader>().AsNoTracking()
                .Where(r => ids.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Name),
            TaskRefTypes.PurchaseOrder => await _db.Set<PurchaseOrder>().AsNoTracking()
                .Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.PONumber),
            TaskRefTypes.Quote => await _db.Set<Quote>().AsNoTracking()
                .Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.QuoteNumber),
            TaskRefTypes.Invoice => await _db.Set<Invoice>().AsNoTracking()
                .Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.InvoiceNumber),
            TaskRefTypes.FinalInvoice => await _db.Set<FinalInvoice>().AsNoTracking()
                .Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.InvoiceNumber),
            _ => [],
        };
    }
}
