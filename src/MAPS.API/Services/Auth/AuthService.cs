using MAPS.API.Data;
using MAPS.API.Data.Entities;
using MAPS.API.Data.Repositories.Interfaces;
using MAPS.Shared.DTOs.Auth;
using MAPS.Shared.DTOs.Common;
using MAPS.Shared.Enums;

namespace MAPS.API.Services.Auth;

public interface IAuthService
{
    Task<ApiResponse<TokenResponse>> LoginAsync(LoginRequest request, string ipAddress);
    Task<ApiResponse<string>>        RegisterAsync(RegisterRequest request);
    Task<ApiResponse<TokenResponse>> RefreshTokenAsync(RefreshTokenRequest request);
    Task<ApiResponse>                RevokeTokenAsync(string refreshToken, Guid userId);
    Task<ApiResponse>                ApproveUserAsync(Guid userId);
}

public class AuthService : IAuthService
{
    private readonly IUserRepository          _userRepo;
    private readonly ITokenService            _tokenService;
    private readonly IRegistrationLockService _lockService;
    private readonly IAuditRepository         _auditRepo;
    private readonly ILogger<AuthService>     _logger;

    public AuthService(
        IUserRepository          userRepo,
        ITokenService            tokenService,
        IRegistrationLockService lockService,
        IAuditRepository         auditRepo,
        ILogger<AuthService>     logger)
    {
        _userRepo     = userRepo;
        _tokenService = tokenService;
        _lockService  = lockService;
        _auditRepo    = auditRepo;
        _logger       = logger;
    }

    // ─── LOGIN ────────────────────────────────────────────────────────────────
    public async Task<ApiResponse<TokenResponse>> LoginAsync(
        LoginRequest request, string ipAddress)
    {
        var user = await _userRepo.GetByEmailAsync(request.Email);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            await _auditRepo.LogAsync(new AuditLog
            {
                UserId     = Guid.Empty,
                Action     = "LOGIN_FAILED",
                EntityType = "Auth",
                EntityId   = request.Email,
                IpAddress  = ipAddress
            });
            return ApiResponse<TokenResponse>.Fail("Invalid email or password.");
        }

        if (!user.IsActive)
            return ApiResponse<TokenResponse>.Fail("Account is deactivated. Contact administrator.");

        if (!user.IsApproved)
            return ApiResponse<TokenResponse>.Fail(
                "Account pending administrator approval. You will be notified once approved.");

        var tokens = _tokenService.GenerateTokens(user);

        // Persist refresh token
        user.RefreshToken       = tokens.RefreshToken;
        user.RefreshTokenExpiry = tokens.RefreshTokenExpiry;
        await _userRepo.UpdateAsync(user);

        await _auditRepo.LogAsync(new AuditLog
        {
            UserId     = user.UserId,
            Action     = "LOGIN_SUCCESS",
            EntityType = "Auth",
            IpAddress  = ipAddress
        });

        _logger.LogInformation("User {Email} logged in successfully", user.Email);
        return ApiResponse<TokenResponse>.Ok(tokens, "Login successful.");
    }

    // ─── REGISTER ─────────────────────────────────────────────────────────────
    public async Task<ApiResponse<string>> RegisterAsync(RegisterRequest request)
    {
        // Registration lock check
        if (_lockService.IsLocked)
            return ApiResponse<string>.Fail(
                "Registration is currently locked. Contact the administrator.");

        if (await _userRepo.EmailExistsAsync(request.Email))
            return ApiResponse<string>.Fail("An account with this email already exists.");

        if (!Enum.TryParse<UserRole>(request.Role, true, out var role) ||
            role == UserRole.Admin)
            return ApiResponse<string>.Fail("Invalid role. Only Doctor or Patient registration is allowed.");

        var user = new AppUser
        {
            FullName     = request.FullName.Trim(),
            Email        = request.Email.Trim().ToLower(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role         = role,
            IsActive     = true,
            IsApproved   = !_lockService.RequiresAdminApproval
        };

        await _userRepo.AddAsync(user);

        // Create role-specific profile
        if (role == UserRole.Doctor)
        {
            // DoctorProfile created via cascade — handled in Chunk 5 (Admin Module)
        }
        else if (role == UserRole.Patient)
        {
            // PatientProfile created — handled in Chunk 7 (Patient Module)
        }

        var message = _lockService.RequiresAdminApproval
            ? "Registration successful. Awaiting administrator approval."
            : "Registration successful. You can now log in.";

        _logger.LogInformation("New {Role} registered: {Email}", role, user.Email);
        return ApiResponse<string>.Ok(user.UserId.ToString(), message);
    }

    // ─── REFRESH TOKEN ────────────────────────────────────────────────────────
    public async Task<ApiResponse<TokenResponse>> RefreshTokenAsync(
        RefreshTokenRequest request)
    {
        var principal = _tokenService.ValidateExpiredToken(request.AccessToken);
        if (principal is null)
            return ApiResponse<TokenResponse>.Fail("Invalid access token.");

        var userIdClaim = principal.FindFirst(MAPS.Shared.Constants.ClaimTypeNames.UserId);
        if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId))
            return ApiResponse<TokenResponse>.Fail("Invalid token claims.");

        var user = await _userRepo.GetByIdAsync(userId);
        if (user is null ||
            user.RefreshToken       != request.RefreshToken ||
            user.RefreshTokenExpiry <= DateTime.UtcNow)
            return ApiResponse<TokenResponse>.Fail("Invalid or expired refresh token.");

        var tokens = _tokenService.GenerateTokens(user);
        user.RefreshToken       = tokens.RefreshToken;
        user.RefreshTokenExpiry = tokens.RefreshTokenExpiry;
        await _userRepo.UpdateAsync(user);

        return ApiResponse<TokenResponse>.Ok(tokens, "Token refreshed successfully.");
    }

    // ─── REVOKE TOKEN ─────────────────────────────────────────────────────────
    public async Task<ApiResponse> RevokeTokenAsync(string refreshToken, Guid userId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user is null)
            return ApiResponse.Fail("User not found.");

        user.RefreshToken       = null;
        user.RefreshTokenExpiry = null;
        await _userRepo.UpdateAsync(user);

        await _auditRepo.LogAsync(new AuditLog
        {
            UserId     = userId,
            Action     = "LOGOUT",
            EntityType = "Auth"
        });

        return ApiResponse.Ok("Logged out successfully.");
    }

    // ─── APPROVE USER ─────────────────────────────────────────────────────────
    public async Task<ApiResponse> ApproveUserAsync(Guid userId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user is null)
            return ApiResponse.Fail("User not found.");

        if (user.IsApproved)
            return ApiResponse.Fail("User is already approved.");

        user.IsApproved = true;
        await _userRepo.UpdateAsync(user);

        _logger.LogInformation("User {UserId} approved", userId);
        return ApiResponse.Ok("User approved successfully.");
    }
}
