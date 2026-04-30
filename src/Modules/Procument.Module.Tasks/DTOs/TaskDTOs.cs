using TaskStatus = Procument.Module.Tasks.Entities.TaskStatus;

namespace Procument.Module.Tasks.DTOs;

public class CreateTaskRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string AssignedTo { get; set; } = string.Empty;
}

public class UpdateTaskStatusRequest
{
    public TaskStatus Status { get; set; }
}

public class UpdateTaskRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string AssignedTo { get; set; } = string.Empty;
}

public class TaskResponse
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string AssignedTo { get; set; } = string.Empty;
    public string CreatedByCode { get; set; } = string.Empty;
    public TaskStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? AssignedToUserName { get; set; }
}
