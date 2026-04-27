using Procument.Module.Identity.Entities;
using Procument.Shared.Entities;

namespace Procument.Module.Tasks.Entities;

public enum TaskStatus
{
    NotStarted = 0,
    InProgress = 1,
    Done = 2
}

public class TaskItem : AuditableEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    // The user's code (GHS, SNP, etc.) or Username
    public string AssignedTo { get; set; } = string.Empty;
    
    // The user's name or code who created it
    public string CreatedByCode { get; set; } = string.Empty;

    public TaskStatus Status { get; set; } = TaskStatus.NotStarted;

    // Optional: Link to actual User entity if we want to be robust
    public long? AssignedToUserId { get; set; }
    public User? AssignedToUser { get; set; }
}
