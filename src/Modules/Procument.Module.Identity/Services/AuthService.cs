using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Procument.Module.Identity.DTOs;
using Procument.Module.Identity.Entities;

namespace Procument.Module.Identity.Services;

public interface IAuthService
{
    Task<AuthSession> LoginAsync(LoginRequest request, string? ipAddress = null);
    Task<AuthSession> RegisterAsync(RegisterRequest request, string? ipAddress = null);
    Task<AuthSession> BootstrapSessionAsync(long userId, string? ipAddress = null);
    Task<AuthSession> RefreshSessionAsync(string refreshToken, string? ipAddress = null);
    Task RevokeRefreshTokenAsync(string refreshToken, string? ipAddress = null);
    Task<UserResponse> AdminCreateUserAsync(AdminCreateUserRequest request);
    Task<List<UserResponse>> GetAllUsersAsync();
    Task<UserResponse?> GetUserByIdAsync(long id);
    Task<bool> ToggleUserActiveAsync(long id);
    Task<bool> UpdateUserAsync(long id, UpdateUserRequest request);
    Task<bool> ChangePasswordAsync(long id, string newPassword);
    Task<ServiceTokenResponse> CreateServiceTokenAsync(ServiceTokenRequest request);
    Task<List<int>> GetUserBasesAsync(long id);
    Task AddUserBaseAsync(long id, int baseValue);
    Task RemoveUserBaseAsync(long id, int baseValue);
    Task<List<long>> GetUserCustomerIdsAsync(long userId);
    Task AddUserCustomerAsync(long userId, long customerId);
    Task RemoveUserCustomerAsync(long userId, long customerId);
}

public class AuthService : IAuthService
{
    private readonly DbContext _db;
    private readonly IConfiguration _config;

    public AuthService(DbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<AuthSession> LoginAsync(LoginRequest request, string? ipAddress = null)
    {
        var user = await _db.Set<User>()
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null)
            throw new UnauthorizedAccessException("Invalid email or password.");

        if (!VerifyPassword(request.Password, user.Password))
            throw new UnauthorizedAccessException("Invalid email or password.");
        if (!user.IsActive)
        {
            throw new UnauthorizedAccessException("Account Deactivated !");
        }
        var bases = await GetUserBasesAsync(user.Id);
        return await CreateSessionAsync(user, bases, ipAddress);
    }

    public async Task<AuthSession> RegisterAsync(RegisterRequest request, string? ipAddress = null)
    {
        await EnsureEmailUnique(request.Email);

        var user = new User
        {
            Name = request.Name,
            Email = request.Email,
            Password = HashPassword(request.Password),
            Role = UserRoles.Expert,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _db.Set<User>().Add(user);
        await _db.SaveChangesAsync();

        var bases = new List<int>(); // newly registered users have no bases
        return await CreateSessionAsync(user, bases, ipAddress);
    }

    public async Task<AuthSession> BootstrapSessionAsync(long userId, string? ipAddress = null)
    {
        var user = await _db.Set<User>().FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null || !user.IsActive)
            throw new UnauthorizedAccessException("Account is unavailable.");

        var bases = await GetUserBasesAsync(user.Id);
        return await CreateSessionAsync(user, bases, ipAddress);
    }

    public async Task<AuthSession> RefreshSessionAsync(string refreshToken, string? ipAddress = null)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new UnauthorizedAccessException("Refresh session is missing.");

        return await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var now = DateTime.UtcNow;
            var tokenHash = HashRefreshToken(refreshToken);
            var stored = await _db.Set<RefreshToken>().Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

            if (stored is null)
                throw new UnauthorizedAccessException("Refresh session is invalid.");

            if (stored.RevokedAt.HasValue)
            {
                // A rotated token being reused may mean it was copied. Revoke the replacement chain.
                await RevokeAllUserSessionsAsync(stored.UserId, now, ipAddress);
                await _db.SaveChangesAsync();
                await transaction.CommitAsync();
                throw new UnauthorizedAccessException("Refresh session has already been used.");
            }

            if (stored.ExpiresAt <= now || !stored.User.IsActive)
            {
                stored.RevokedAt = now;
                stored.RevokedByIp = ipAddress;
                await _db.SaveChangesAsync();
                await transaction.CommitAsync();
                throw new UnauthorizedAccessException("Refresh session has expired.");
            }

            var bases = await GetUserBasesAsync(stored.UserId);
            var next = NewRefreshToken(stored.UserId, ipAddress);
            stored.RevokedAt = now;
            stored.RevokedByIp = ipAddress;
            stored.ReplacedByTokenHash = next.Entity.TokenHash;
            _db.Set<RefreshToken>().Add(next.Entity);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return new AuthSession
            {
                Response = await BuildAuthResponseAsync(stored.User, bases),
                RefreshToken = next.RawToken,
                RefreshTokenExpiresAt = next.Entity.ExpiresAt,
            };
        });
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken, string? ipAddress = null)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;
        var hash = HashRefreshToken(refreshToken);
        var stored = await _db.Set<RefreshToken>().FirstOrDefaultAsync(t => t.TokenHash == hash);
        if (stored is null || stored.RevokedAt.HasValue) return;
        stored.RevokedAt = DateTime.UtcNow;
        stored.RevokedByIp = ipAddress;
        await _db.SaveChangesAsync();
    }

    public async Task<UserResponse> AdminCreateUserAsync(AdminCreateUserRequest request)
    {
        await EnsureEmailUnique(request.Email);

        // Validate role
        var allowedRoles = new[] { UserRoles.Admin, UserRoles.Expert, UserRoles.SuperAdmin, UserRoles.Payment, UserRoles.AHM };
        if (!allowedRoles.Contains(request.Role))
            throw new ArgumentException($"Invalid role. Must be one of: {string.Join(", ", allowedRoles)}.");

        var user = new User
        {
            Name = request.Name,
            Email = request.Email,
            Password = HashPassword(request.Password),
            Role = request.Role,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _db.Set<User>().Add(user);
        await _db.SaveChangesAsync();

        return MapToUserResponse(user);
    }

    public async Task<List<UserResponse>> GetAllUsersAsync()
    {
        return await _db.Set<User>()
            .Include(u => u.UserBases)
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new UserResponse
            {
                Id = u.Id,
                Name = u.Name,
                Email = u.Email,
                Role = u.Role,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
                Bases = u.UserBases.Select(ub => ub.Base).OrderBy(b => b).ToList()
            })
            .ToListAsync();
    }

    public async Task<UserResponse?> GetUserByIdAsync(long id)
    {
        var user = await _db.Set<User>().FindAsync(id);
        return user == null ? null : MapToUserResponse(user);
    }

    public async Task<bool> ToggleUserActiveAsync(long id)
    {
        var user = await _db.Set<User>().FindAsync(id);
        if (user == null) return false;

        user.IsActive = !user.IsActive;
        user.ModifyAt = DateTime.UtcNow;
        if (!user.IsActive)
            await RevokeAllUserSessionsAsync(user.Id, DateTime.UtcNow, null);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateUserAsync(long id, UpdateUserRequest request)
    {
        var user = await _db.Set<User>().FindAsync(id);
        if (user == null) return false;

        // Ensure email uniqueness if changed
        if (user.Email != request.Email)
        {
            await EnsureEmailUnique(request.Email);
        }

        user.Name = request.Name;
        user.Email = request.Email;
        user.Role = request.Role;
        user.ModifyAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ChangePasswordAsync(long id, string newPassword)
    {
        var user = await _db.Set<User>().FindAsync(id);
        if (user == null) return false;

        user.Password = HashPassword(newPassword);
        user.ModifyAt = DateTime.UtcNow;
        await RevokeAllUserSessionsAsync(user.Id, DateTime.UtcNow, null);

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task AddUserBaseAsync(long id, int baseValue)
    {
        var exists = await _db.Set<UserBase>()
            .AnyAsync(ub => ub.UserId == id && ub.Base == baseValue);
        if (exists) return;
        _db.Set<UserBase>().Add(new UserBase { UserId = id, Base = baseValue });
        await _db.SaveChangesAsync();
    }

    public async Task RemoveUserBaseAsync(long id, int baseValue)
    {
        var entry = await _db.Set<UserBase>()
            .FirstOrDefaultAsync(ub => ub.UserId == id && ub.Base == baseValue);
        if (entry == null) return;
        _db.Set<UserBase>().Remove(entry);
        await _db.SaveChangesAsync();
    }

    // ────── Private Helpers ──────

    private async Task EnsureEmailUnique(string email)
    {
        if (await _db.Set<User>().AnyAsync(u => u.Email == email))
            throw new InvalidOperationException("A user with this email already exists.");
    }

    private static UserResponse MapToUserResponse(User user) => new()
    {
        Id = user.Id,
        Name = user.Name,
        Email = user.Email,
        Role = user.Role,
        IsActive = user.IsActive,
        CreatedAt = user.CreatedAt
    };

    /// <summary>
    /// Mints a long-lived bearer token for a machine client. The claims are identical to a login
    /// token — same NameIdentifier/Role/bases shape — so every existing [Authorize] rule and
    /// GetUserContext() call treats the robot exactly like the user it acts as.
    /// NOTE: nothing here is revocable. The token stays valid until it expires; the only way to
    /// kill it early is rotating JwtSettings:Secret, which also signs out every human user.
    /// Keep the lifetime as short as the robot's job allows and give it the narrowest role.
    /// </summary>
    public async Task<ServiceTokenResponse> CreateServiceTokenAsync(ServiceTokenRequest request)
    {
        var user = await _db.Set<User>().FirstOrDefaultAsync(u => u.Id == request.UserId)
            ?? throw new Exception($"User {request.UserId} does not exist.");

        if (!user.IsActive)
            throw new Exception($"User '{user.Name}' is inactive. Activate the account before issuing a token for it.");

        var days = request.Days <= 0 ? 365 : Math.Min(request.Days, 3650);
        var bases = await GetUserBasesAsync(user.Id);
        var token = await GenerateJwtTokenAsync(user, bases, TimeSpan.FromDays(days));

        return new ServiceTokenResponse
        {
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddDays(days),
            UserId = user.Id,
            UserName = user.Name,
            Role = user.Role,
            Bases = bases
        };
    }

    private async Task<string> GenerateJwtTokenAsync(User user, List<int>? bases = null, TimeSpan? lifetime = null)
    {
        bases ??= await GetUserBasesAsync(user.Id);

        var jwtSettings = _config.GetSection("JwtSettings");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Secret"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("bases", string.Join(",", bases))
        };

        var expMinutes = int.Parse(jwtSettings["ExpirationInMinutes"] ?? "10080");
        var expiresAt = DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(expMinutes));

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<AuthSession> CreateSessionAsync(User user, List<int> bases, string? ipAddress)
    {
        var refresh = NewRefreshToken(user.Id, ipAddress);
        _db.Set<RefreshToken>().Add(refresh.Entity);
        await _db.SaveChangesAsync();
        return new AuthSession
        {
            Response = await BuildAuthResponseAsync(user, bases),
            RefreshToken = refresh.RawToken,
            RefreshTokenExpiresAt = refresh.Entity.ExpiresAt,
        };
    }

    private async Task<AuthResponse> BuildAuthResponseAsync(User user, List<int> bases) => new()
    {
        Id = user.Id,
        Name = user.Name,
        Email = user.Email,
        Role = user.Role,
        Token = await GenerateJwtTokenAsync(user, bases),
        Bases = bases,
    };

    private (RefreshToken Entity, string RawToken) NewRefreshToken(long userId, string? ipAddress)
    {
        var raw = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));
        var days = int.Parse(_config["JwtSettings:RefreshTokenDays"] ?? "30");
        return (new RefreshToken
        {
            UserId = userId,
            TokenHash = HashRefreshToken(raw),
            ExpiresAt = DateTime.UtcNow.AddDays(days),
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = ipAddress,
        }, raw);
    }

    private async Task RevokeAllUserSessionsAsync(long userId, DateTime revokedAt, string? ipAddress)
    {
        var active = await _db.Set<RefreshToken>()
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > revokedAt)
            .ToListAsync();
        foreach (var token in active)
        {
            token.RevokedAt = revokedAt;
            token.RevokedByIp = ipAddress;
        }
    }

    private static string HashRefreshToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public async Task<List<int>> GetUserBasesAsync(long userId)
    {
        return await _db.Set<UserBase>()
            .Where(ub => ub.UserId == userId)
            .OrderBy(ub => ub.Base)
            .Select(ub => ub.Base)
            .ToListAsync();
    }

    public async Task<List<long>> GetUserCustomerIdsAsync(long userId)
    {
        return await _db.Set<UserCustomer>()
            .Where(uc => uc.UserId == userId)
            .Select(uc => uc.CustomerId)
            .ToListAsync();
    }

    public async Task AddUserCustomerAsync(long userId, long customerId)
    {
        var exists = await _db.Set<UserCustomer>()
            .AnyAsync(uc => uc.UserId == userId && uc.CustomerId == customerId);
        if (exists) return;
        _db.Set<UserCustomer>().Add(new UserCustomer { UserId = userId, CustomerId = customerId });
        await _db.SaveChangesAsync();
    }

    public async Task RemoveUserCustomerAsync(long userId, long customerId)
    {
        var entry = await _db.Set<UserCustomer>()
            .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.CustomerId == customerId);
        if (entry == null) return;
        _db.Set<UserCustomer>().Remove(entry);
        await _db.SaveChangesAsync();
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations: 100_000,
            HashAlgorithmName.SHA256,
            outputLength: 32);

        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string storedHash)
    {
        var parts = storedHash.Split('.');
        if (parts.Length != 2) return false;

        var salt = Convert.FromBase64String(parts[0]);
        var hash = Convert.FromBase64String(parts[1]);

        var testHash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations: 100_000,
            HashAlgorithmName.SHA256,
            outputLength: 32);

        return CryptographicOperations.FixedTimeEquals(hash, testHash);
    }
}
