namespace Procument.Module.Identity.DTOs;

public class AssignPermissionRequest
{
    public long UserId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Permission { get; set; } = string.Empty;
}
