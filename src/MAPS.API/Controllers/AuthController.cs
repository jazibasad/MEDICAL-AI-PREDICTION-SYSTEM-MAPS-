using MAPS.API.Services.Auth;
using MAPS.Shared.DTOs.Auth;
using MAPS.Shared.DTOs.Common;

namespace MAPS.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>POST /api/auth/login</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var ip     = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var result = await _authService.LoginAsync(request, ip);
        return result.Success ? Ok(result) : Unauthorized(result);
    }

    /// <summary>POST /api/auth/register</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>POST /api/auth/refresh</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        var result = await _authService.RefreshTokenAsync(request);
        return result.Success ? Ok(result) : Unauthorized(result);
    }

    /// <summary>POST /api/auth/revoke — logout</summary>
    [HttpPost("revoke")]
    [Authorize]
    public async Task<IActionResult> Revoke([FromBody] RevokeTokenRequest request)
    {
        var userId = Guid.Parse(User.FindFirst(
            MAPS.Shared.Constants.ClaimTypeNames.UserId)!.Value);
        var result = await _authService.RevokeTokenAsync(request.RefreshToken, userId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>POST /api/auth/approve/{userId} — Admin only</summary>
    [HttpPost("approve/{userId:guid}")]
    [Authorize(Policy = PolicyNames.AdminOnly)]
    public async Task<IActionResult> Approve(Guid userId)
    {
        var result = await _authService.ApproveUserAsync(userId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>GET /api/auth/me — current user info</summary>
    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var claims = new
        {
            UserId   = User.FindFirst(ClaimTypeNames.UserId)?.Value,
            Email    = User.FindFirst(ClaimTypeNames.Email)?.Value,
            FullName = User.FindFirst(ClaimTypeNames.FullName)?.Value,
            Role     = User.FindFirst(ClaimTypeNames.Role)?.Value
        };
        return Ok(ApiResponse<object>.Ok(claims));
    }
}
