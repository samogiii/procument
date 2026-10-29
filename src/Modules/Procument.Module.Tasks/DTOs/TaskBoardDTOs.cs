namespace Procument.Module.Tasks.DTOs;

public class TaskBoardResponse
{
    public long OwnerUserId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    /// <summary>The server's calendar day (yyyy-MM-dd) — due dates and "overdue" are measured against it.</summary>
    public string ServerToday { get; set; } = string.Empty;
    /// <summary>Viewer is looking at someone else's board (SuperAdmin).</summary>
    public bool IsOtherUser { get; set; }
    public List<TaskColumnResponse> Columns { get; set; } = [];
    public List<TaskTagResponse> Tags { get; set; } = [];
}

public class TaskColumnResponse
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public int Position { get; set; }
    public bool IsDone { get; set; }
    /// <summary>Cards in order.</summary>
    public List<TaskCardResponse> Cards { get; set; } = [];
}

public class TaskTagResponse
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    /// <summary>Cards using the tag.</summary>
    public int Usage { get; set; }
}

public class TaskRefResponse
{
    public string Type { get; set; } = string.Empty;
    public long Id { get; set; }
    /// <summary>Document number ("#id" when it was deleted since).</summary>
    public string Label { get; set; } = string.Empty;
    public bool Missing { get; set; }
}

public class TaskLastComment
{
    public string Text { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>What a board card shows.</summary>
public class TaskCardResponse
{
    public long Id { get; set; }
    public long ColumnId { get; set; }
    public int Position { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    /// <summary>yyyy-MM-dd or null.</summary>
    public string? DueDate { get; set; }
    public bool HasDescription { get; set; }
    public List<long> TagIds { get; set; } = [];
    public List<TaskRefResponse> Refs { get; set; } = [];
    public TaskLastComment? LastComment { get; set; }
    public int CommentCount { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class TaskActivityResponse
{
    public long Id { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>The card modal: everything a board card has plus the description and the whole timeline (newest first).</summary>
public class TaskCardDetailResponse
{
    public TaskCardResponse Card { get; set; } = new();
    public string? Description { get; set; }
    public List<TaskActivityResponse> Activities { get; set; } = [];
    /// <summary>Tags may have been created or dropped by the change — the board refreshes its tag list from here.</summary>
    public List<TaskTagResponse> Tags { get; set; } = [];
}

public class CreateTaskColumnRequest
{
    public long? UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
}

public class UpdateTaskColumnRequest
{
    public string? Name { get; set; }
    public string? Color { get; set; }
    public bool? IsDone { get; set; }
}

public class ReorderTaskColumnsRequest
{
    public long? UserId { get; set; }
    public List<long> Ids { get; set; } = [];
}

public class CreateTaskCardRequest
{
    public long ColumnId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Priority { get; set; }
    /// <summary>yyyy-MM-dd.</summary>
    public string? DueDate { get; set; }
    /// <summary>Put the card at the top of the column (default: bottom).</summary>
    public bool Top { get; set; }
}

/// <summary>One field of a card: title | description | priority | dueDate (yyyy-MM-dd, null clears).</summary>
public class UpdateTaskCardFieldRequest
{
    public string Field { get; set; } = string.Empty;
    public string? Value { get; set; }
}

public class MoveTaskCardRequest
{
    public long ColumnId { get; set; }
    public int Index { get; set; }
}

public class SetTaskCardTagsRequest
{
    /// <summary>Tag names; unknown names create new tags.</summary>
    public List<string> Names { get; set; } = [];
}

public class TaskRefKey
{
    public string Type { get; set; } = string.Empty;
    public long Id { get; set; }
}

public class SetTaskCardRefsRequest
{
    public List<TaskRefKey> Refs { get; set; } = [];
}

public class AddTaskCommentRequest
{
    public string Text { get; set; } = string.Empty;
}

public class TaskBoardUserResponse
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    /// <summary>Cards on the user's board (0 when it was never opened).</summary>
    public int CardCount { get; set; }
}
