using MAPS.API.Data.Entities;
using MAPS.API.Data.Repositories.Interfaces;
using MAPS.Shared.Constants;

namespace MAPS.API.Middleware;

public class AuditLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditLoggingMiddleware> _logger;

    // Paths to skip auditing (health checks, swagger, static files)
    private static readonly HashSet<string> _skipPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/health", "/swagger", "/favicon.ico"
    };

    public AuditLoggingMiddleware(
        RequestDelegate next,
        ILogger<AuditLoggingMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IAuditRepository auditRepo)
    {
        var path = context.Request.Path.Value ?? "";

        // Skip non-API or excluded paths
        if (!path.StartsWith("/api") ||
            _skipPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        await _next(context);

        // Only log authenticated requests
        if (!context.User.Identity?.IsAuthenticated ?? true) return;

        var userIdClaim = context.User.FindFirst(ClaimTypeNames.UserId);
        if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId)) return;

        try
        {
            var action = $"{context.Request.Method}:{path}:{context.Response.StatusCode}";
            var entityType = ExtractEntityType(path);

            await auditRepo.LogAsync(new AuditLog
            {
                UserId     = userId,
                Action     = action,
                EntityType = entityType,
                EntityId   = ExtractEntityId(path),
                IpAddress  = context.Connection.RemoteIpAddress?.ToString()
            });
        }
        catch (Exception ex)
        {
            // Audit logging must never break the request pipeline
            _logger.LogWarning(ex, "Audit logging failed for {Path}", path);
        }
    }

    private static string ExtractEntityType(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 2 ? segments[1] : "Unknown";
    }

    private static string? ExtractEntityId(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 3 &&
               Guid.TryParse(segments[2], out _)
            ? segments[2]
            : null;
    }
}
