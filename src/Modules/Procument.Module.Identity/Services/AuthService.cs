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
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<UserResponse> AdminCreateUserAsync(AdminCreateUserRequest request);
    Task<List<UserResponse>> GetAllUsersAsync();
    Task<UserResponse?> GetUserByIdAsync(long id);
    Task<bool> ToggleUserActiveAsync(long id);
    Task<bool> UpdateUserAsync(long id, UpdateUserRequest request);
    Task<bool> ChangePasswordAsync(long id, string newPassword);
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

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
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
        return new AuthResponse
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role,
            Token = await GenerateJwtTokenAsync(user, bases),
            Bases = bases
        };
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
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
        return new AuthResponse
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role,
            Token = await GenerateJwtTokenAsync(user, bases),
            Bases = bases
        };
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

    private async Task<string> GenerateJwtTokenAsync(User user, List<int>? bases = null)
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

        var expMinutes = int.Parse(jwtSettings["ExpirationInMinutes"] ?? "480");

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

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
