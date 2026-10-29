using Procument.Module.Identity.Entities;
using Procument.Shared.Entities;

namespace Procument.Module.Tasks.Entities;

/// <summary>
/// A column of one user's kanban board. Every user has their own columns; the defaults are created
/// the first time the board opens (see TaskBoardService.EnsureBoardAsync).
/// </summary>
public class TaskBoardColumn : BaseEntity
{
    public long OwnerUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Hex color of the column header, e.g. "#5B4FD8".</summary>
    public string Color { get; set; } = "#6E7681";
    public int Position { get; set; }
    /// <summary>Cards here count as finished (no overdue warning, not in the nav badge).</summary>
    public bool IsDone { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User OwnerUser { get; set; } = null!;
    public ICollection<TaskCard> Cards { get; set; } = [];
}

/// <summary>One card of a kanban board. OwnerUserId always equals its column's owner.</summary>
public class TaskCard : BaseEntity
{
    public long OwnerUserId { get; set; }
    public long ColumnId { get; set; }
    /// <summary>Order inside the column, 0 first.</summary>
    public int Position { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>One of <see cref="TaskPriorities"/>.</summary>
    public string Priority { get; set; } = TaskPriorities.Normal;
    /// <summary>Date only (server calendar day).</summary>
    public DateTime? DueDate { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public TaskBoardColumn Column { get; set; } = null!;
    public ICollection<TaskCardTag> Tags { get; set; } = [];
    public ICollection<TaskCardRef> Refs { get; set; } = [];
    public ICollection<TaskCardActivity> Activities { get; set; } = [];
}

public static class TaskPriorities
{
    public const string Aog = "AOG";
    public const string High = "High";
    public const string Normal = "Normal";

    public static readonly string[] All = [Aog, High, Normal];

    public static string? Normalize(string? value) =>
        All.FirstOrDefault(p => string.Equals(p, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// A tag of one user's board. Created the first time a card uses the name; deleted as soon as no card uses it.
/// </summary>
public class TaskTag : BaseEntity
{
    public long OwnerUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#1565C0";

    public ICollection<TaskCardTag> Cards { get; set; } = [];
}

public class TaskCardTag
{
    public long CardId { get; set; }
    public long TagId { get; set; }

    public TaskCard Card { get; set; } = null!;
    public TaskTag Tag { get; set; } = null!;
}

/// <summary>
/// A Reference of a card: RFQ, purchase order, quote, proforma invoice or final invoice.
/// RefId is a plain column (no FK) — the documents live in other modules.
/// </summary>
public class TaskCardRef : BaseEntity
{
    public long CardId { get; set; }
    /// <summary>One of <see cref="TaskRefTypes"/>.</summary>
    public string RefType { get; set; } = string.Empty;
    public long RefId { get; set; }

    public TaskCard Card { get; set; } = null!;
}

public static class TaskRefTypes
{
    public const string Rfq = "Rfq";
    public const string PurchaseOrder = "PurchaseOrder";
    public const string Quote = "Quote";
    public const string Invoice = "Invoice";
    public const string FinalInvoice = "FinalInvoice";

    public static readonly string[] All = [Rfq, Quote, Invoice, PurchaseOrder, FinalInvoice];

    public static string? Normalize(string? type) =>
        All.FirstOrDefault(t => string.Equals(t, type?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string Short(string type) => type switch
    {
        Rfq => "RFQ",
        PurchaseOrder => "PO",
        Invoice => "PI",
        FinalInvoice => "FI",
        _ => type,
    };
}

/// <summary>
/// Timeline entry of a card: a comment the user wrote, or a change (created, moved, title, tags, refs …).
/// </summary>
public class TaskCardActivity : BaseEntity
{
    public long CardId { get; set; }
    /// <summary>One of <see cref="TaskActivityKinds"/>.</summary>
    public string Kind { get; set; } = TaskActivityKinds.Comment;
    public string Text { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public TaskCard Card { get; set; } = null!;
}

public static class TaskActivityKinds
{
    public const string Comment = "Comment";
    public const string Created = "Created";
    public const string Moved = "Moved";
    public const string Title = "Title";
    public const string Description = "Description";
    public const string Priority = "Priority";
    public const string DueDate = "DueDate";
    public const string Tag = "Tag";
    public const string Ref = "Ref";
}
