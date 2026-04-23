using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procument.Module.Identity.DTOs;
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

    public UsersController(IAuthService authService)
    {
        _authService = authService;
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
}
