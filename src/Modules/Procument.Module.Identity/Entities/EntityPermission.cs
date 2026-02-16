using Procument.Shared.Entities;

namespace Procument.Module.Identity.Entities;

public class EntityPermission : BaseEntity
{
    public long UserId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Permission { get; set; } = string.Empty;

    // Navigation
    public User User { get; set; } = null!;
}
