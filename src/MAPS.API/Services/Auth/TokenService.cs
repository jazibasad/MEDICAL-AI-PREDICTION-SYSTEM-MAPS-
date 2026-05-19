using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using MAPS.API.Data.Entities;
using MAPS.Shared.Constants;
using MAPS.Shared.DTOs.Auth;

namespace MAPS.API.Services.Auth;

public interface ITokenService
{
    TokenResponse GenerateTokens(AppUser user);
    ClaimsPrincipal? ValidateExpiredToken(string token);
    string GenerateRefreshToken();
}

public class TokenService : ITokenService
{
    private readonly IConfiguration _config;

    public TokenService(IConfiguration config)
    {
        _config = config;
    }

    public TokenResponse GenerateTokens(AppUser user)
    {
        var accessToken  = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken();
        var accessExpiry = DateTime.UtcNow.AddMinutes(
            _config.GetValue<int>("JwtSettings:AccessTokenExpiryMinutes"));
        var refreshExpiry = DateTime.UtcNow.AddDays(
            _config.GetValue<int>("JwtSettings:RefreshTokenExpiryDays"));

        return new TokenResponse
        {
            AccessToken        = accessToken,
            RefreshToken       = refreshToken,
            AccessTokenExpiry  = accessExpiry,
            RefreshTokenExpiry = refreshExpiry,
            Role               = user.Role.ToString(),
            UserId             = user.UserId.ToString(),
            FullName           = user.FullName,
            Email              = user.Email
        };
    }

    private string GenerateAccessToken(AppUser user)
    {
        var key     = new SymmetricSecurityKey(
                          Encoding.UTF8.GetBytes(
                              _config["JwtSettings:SecretKey"]!));
        var creds   = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry  = DateTime.UtcNow.AddMinutes(
                          _config.GetValue<int>("JwtSettings:AccessTokenExpiryMinutes"));

        var claims = new[]
        {
            new Claim(ClaimTypeNames.UserId,     user.UserId.ToString()),
            new Claim(ClaimTypeNames.Email,      user.Email),
            new Claim(ClaimTypeNames.FullName,   user.FullName),
            new Claim(ClaimTypeNames.Role,       user.Role.ToString()),
            new Claim(ClaimTypeNames.IsApproved, user.IsApproved.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat,
                      DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                      ClaimValueTypes.Integer64)
        };

        var token = new JwtSecurityToken(
            issuer:   _config["JwtSettings:Issuer"],
            audience: _config["JwtSettings:Audience"],
            claims:   claims,
            expires:  expiry,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng   = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    public ClaimsPrincipal? ValidateExpiredToken(string token)
    {
        var tokenValidationParams = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = _config["JwtSettings:Issuer"],
            ValidAudience            = _config["JwtSettings:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(
                                           Encoding.UTF8.GetBytes(
                                               _config["JwtSettings:SecretKey"]!)),
            ValidateLifetime         = false // Allow expired tokens for refresh
        };

        try
        {
            var principal = new JwtSecurityTokenHandler()
                .ValidateToken(token, tokenValidationParams, out var validatedToken);

            if (validatedToken is not JwtSecurityToken jwtToken ||
                !jwtToken.Header.Alg.Equals(
                    SecurityAlgorithms.HmacSha256,
                    StringComparison.InvariantCultureIgnoreCase))
                return null;

            return principal;
        }
        catch
        {
            return null;
        }
    }
}
