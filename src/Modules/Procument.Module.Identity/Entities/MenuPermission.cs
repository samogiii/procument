using Procument.Shared.Entities;

namespace Procument.Module.Identity.Entities;

/// <summary>
/// Grants a named user access to a specific gated menu/feature.
/// SuperAdmin always has access regardless of this table.
/// </summary>
public class MenuPermission : BaseEntity
{
    /// <summary>Feature key matching FeaturePermissions in the frontend auth store (e.g. "paymentMenu").</summary>
    public string Feature { get; set; } = string.Empty;

    /// <summary>The user's display name (User.Name).</summary>
    public string UserName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
