using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Identity.DTOs;
using Procument.Module.Identity.Entities;
using Procument.Module.Identity.Services;

using Procument.Shared.Audit;

namespace Procument.Module.Identity.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>Login with email and password.</summary>
    [HttpPost("login")]
    [Auditable("User", "Login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            var result = await _authService.LoginAsync(request);
            // Audit handled by filter
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    /// <summary>Self-register as an Expert.</summary>
    [HttpPost("register")]
    [Auditable("User", "Register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var result = await _authService.RegisterAsync(request);
            // Audit handled by filter
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class UsersController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly DbContext _db;

    public UsersController(IAuthService authService, DbContext db)
    {
        _authService = authService;
        _db = db;
    }

    /// <summary>Admin: create a new Expert or Admin user.</summary>
    [HttpPost]
    public async Task<ActionResult<UserResponse>> CreateUser([FromBody] AdminCreateUserRequest request)
    {
        try
        {
            var result = await _authService.AdminCreateUserAsync(request);
            return CreatedAtAction(nameof(GetUser), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Admin: list all users.</summary>
    [HttpGet]
    public async Task<ActionResult<List<UserResponse>>> GetUsers()
    {
        var users = await _authService.GetAllUsersAsync();
        return Ok(users);
    }

    /// <summary>Admin: get user by ID.</summary>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<UserResponse>> GetUser(long id)
    {
        var user = await _authService.GetUserByIdAsync(id);
        return user == null ? NotFound() : Ok(user);
    }

    /// <summary>Admin: activate/deactivate a user.</summary>
    [HttpPatch("{id:long}/toggle-active")]
    public async Task<ActionResult> ToggleActive(long id)
    {
        var success = await _authService.ToggleUserActiveAsync(id);
        return success ? Ok(new { message = "User status toggled." }) : NotFound();
    }

    /// <summary>Admin: update user information and role.</summary>
    [HttpPut("{id:long}")]
    public async Task<ActionResult> UpdateUser(long id, [FromBody] UpdateUserRequest request)
    {
        try
        {
            var success = await _authService.UpdateUserAsync(id, request);
            return success ? Ok(new { message = "User updated." }) : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>Admin: change user password.</summary>
    [HttpPatch("{id:long}/password")]
    public async Task<ActionResult> ChangePassword(long id, [FromBody] AdminChangePasswordRequest request)
    {
        var success = await _authService.ChangePasswordAsync(id, request.NewPassword);
        return success ? Ok(new { message = "Password changed." }) : NotFound();
    }

    /// <summary>Admin: get bases assigned to a user.</summary>
    [HttpGet("{id:long}/bases")]
    public async Task<ActionResult<List<int>>> GetUserBases(long id)
    {
        var bases = await _authService.GetUserBasesAsync(id);
        return Ok(bases);
    }

    /// <summary>Admin: add a base to a user.</summary>
    [HttpPost("{id:long}/bases")]
    public async Task<ActionResult> AddUserBase(long id, [FromBody] AddUserBaseRequest request)
    {
        await _authService.AddUserBaseAsync(id, request.Base);
        return Ok(new { message = "Base added." });
    }

    /// <summary>Admin: remove a base from a user.</summary>
    [HttpDelete("{id:long}/bases/{baseValue:int}")]
    public async Task<ActionResult> RemoveUserBase(long id, int baseValue)
    {
        await _authService.RemoveUserBaseAsync(id, baseValue);
        return Ok(new { message = "Base removed." });
    }

    /// <summary>Admin: get individually-assigned customers for a user (returns id, name, customerCode).</summary>
    [HttpGet("{id:long}/customers")]
    public async Task<ActionResult> GetUserCustomers(long id)
    {
        var customerIds = await _authService.GetUserCustomerIdsAsync(id);
        if (customerIds.Count == 0) return Ok(Array.Empty<object>());

        // Raw SQL to avoid cross-module entity import
        var conn = _db.Database.GetDbConnection();
        var wasOpen = conn.State == System.Data.ConnectionState.Open;
        if (!wasOpen) await conn.OpenAsync();
        try
        {
            var idList = string.Join(",", customerIds);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT Id, Name, CustomerCode FROM Customers WHERE Id IN ({idList}) ORDER BY Name";
            using var reader = await cmd.ExecuteReaderAsync();
            var results = new List<object>();
            while (await reader.ReadAsync())
            {
                results.Add(new
                {
                    id = reader.GetInt64(0),
                    name = reader.GetString(1),
                    customerCode = reader.IsDBNull(2) ? null : reader.GetString(2),
                });
            }
            return Ok(results);
        }
        finally
        {
            if (!wasOpen) await conn.CloseAsync();
        }
    }

    /// <summary>Admin: individually assign a customer to a user.</summary>
    [HttpPost("{id:long}/customers")]
    public async Task<ActionResult> AddUserCustomer(long id, [FromBody] AddUserCustomerRequest request)
    {
        await _authService.AddUserCustomerAsync(id, request.CustomerId);
        return Ok(new { message = "Customer assigned." });
    }

    /// <summary>Admin: remove an individually assigned customer from a user.</summary>
    [HttpDelete("{id:long}/customers/{customerId:long}")]
    public async Task<ActionResult> RemoveUserCustomer(long id, long customerId)
    {
        await _authService.RemoveUserCustomerAsync(id, customerId);
        return Ok(new { message = "Customer removed." });
    }
}

/// <summary>
/// SuperAdmin-only controller for managing which users can see each gated menu/feature.
/// GET  /api/menu-permissions         → grouped list [{ feature, userNames[] }]
/// POST /api/menu-permissions         → add a user to a feature
/// DELETE /api/menu-permissions/{feature}/{userName} → remove a user from a feature
/// </summary>
[ApiController]
[Route("api/menu-permissions")]
[Authorize]   // GET is open to all authenticated users; POST/DELETE enforce SuperAdmin below
public class MenuPermissionsController : ControllerBase
{
    private readonly DbContext _db;

    public MenuPermissionsController(DbContext db)
    {
        _db = db;
    }

    private static readonly string[] KnownFeatures =
    [
        "paymentMenu", "companyPresets", "syncApp", "systemActivity",
        "supplierRequests", "capList", "ils", "shippingMenu",
        "customerMenu", "isAmir", "newRFQ", "ilsUsers", "isPDFSelection"
    ];

    /// <summary>Returns all features with their currently allowed user names.</summary>
    [HttpGet]
    public async Task<ActionResult<List<MenuPermissionGroupResponse>>> GetAll()
    {
        var rows = await _db.Set<MenuPermission>().AsNoTracking().ToListAsync();

        var result = KnownFeatures.Select(f => new MenuPermissionGroupResponse
        {
            Feature = f,
            UserNames = rows.Where(r => r.Feature == f).Select(r => r.UserName).OrderBy(n => n).ToList()
        }).ToList();

        return Ok(result);
    }

    /// <summary>Grant a user access to a feature.</summary>
    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult> Add([FromBody] AddMenuPermissionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Feature) || string.IsNullOrWhiteSpace(request.UserName))
            return BadRequest(new { message = "Feature and UserName are required." });

        var exists = await _db.Set<MenuPermission>()
            .AnyAsync(p => p.Feature == request.Feature && p.UserName == request.UserName);
        if (exists) return Ok(new { message = "Already granted." });

        _db.Set<MenuPermission>().Add(new MenuPermission
        {
            Feature = request.Feature,
            UserName = request.UserName,
        });
        await _db.SaveChangesAsync();
        return Ok(new { message = "Permission granted." });
    }

    /// <summary>Revoke a user's access to a feature.</summary>
    [HttpDelete("{feature}/{userName}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult> Remove(string feature, string userName)
    {
        var row = await _db.Set<MenuPermission>()
            .FirstOrDefaultAsync(p => p.Feature == feature && p.UserName == userName);
        if (row == null) return NotFound();

        _db.Set<MenuPermission>().Remove(row);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Permission revoked." });
    }
}
