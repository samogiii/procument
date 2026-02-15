using Procument.Shared.Entities;

namespace Procument.Module.Identity.Entities;

public class User : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = UserRoles.Expert;
}

public static class UserRoles
{
    public const string Admin = "Admin";
    public const string Expert = "Expert";
}
