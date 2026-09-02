namespace Procument.Module.Identity.DTOs;

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RegisterRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class AdminCreateUserRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = "Expert";
}

public class UpdateUserRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class AdminChangePasswordRequest
{
    public string NewPassword { get; set; } = string.Empty;
}

public class AuthResponse
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public List<int> Bases { get; set; } = new();
}

public class UserResponse
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? AssignedAt { get; set; }
    public List<int> Bases { get; set; } = new();
}

public class AddUserBaseRequest
{
    public int Base { get; set; }
}

public class AddUserCustomerRequest
{
    public long CustomerId { get; set; }
}

// ── Menu Permissions ──────────────────────────────────────────────────────────

/// <summary>One gated menu/feature with its currently allowed user names.</summary>
public class MenuPermissionGroupResponse
{
    public string Feature { get; set; } = string.Empty;
    public List<string> UserNames { get; set; } = new();
}

public class AddMenuPermissionRequest
{
    public string Feature { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
}

/// <summary>Request a long-lived bearer token for a machine client ("robot") that acts as an existing user.</summary>
public class ServiceTokenRequest
{
    /// <summary>The user the robot acts as. Its role and bases decide what the token can do.</summary>
    public long UserId { get; set; }
    /// <summary>Token lifetime in days. Defaults to 365, capped at 3650.</summary>
    public int Days { get; set; } = 365;
    /// <summary>Free-text note recorded in the audit log, e.g. "nightly RFQ importer".</summary>
    public string? Label { get; set; }
}

public class ServiceTokenResponse
{
    /// <summary>The bearer token. Shown once here — it is not stored and cannot be retrieved again.</summary>
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public long UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public List<int> Bases { get; set; } = new();
}
