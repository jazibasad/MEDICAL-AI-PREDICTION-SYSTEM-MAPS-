using System.Collections.Concurrent;
using MAPS.Shared.Constants;

namespace MAPS.API.Middleware;

/// <summary>
/// Sliding window rate limiter.
/// Limits per-IP (unauthenticated) and per-user (authenticated).
/// Auth endpoints get tighter limits to prevent brute-force.
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;

    // Thread-safe request tracking: key → list of request timestamps
    private static readonly ConcurrentDictionary<string, List<DateTime>> _requests = new();
    private static readonly object _lock = new();

    // Endpoint-specific limits
    private static readonly Dictionary<string, (int Limit, TimeSpan Window)> EndpointLimits = new()
    {
        ["/api/auth/login"]    = (10,  TimeSpan.FromMinutes(15)), // 10 per 15 min
        ["/api/auth/register"] = (5,   TimeSpan.FromHours(1)),    // 5 per hour
        ["/api/auth/refresh"]  = (20,  TimeSpan.FromMinutes(15)), // 20 per 15 min
        ["/api/predictions"]   = (50,  TimeSpan.FromMinutes(1)),  // 50 per minute
        ["/api/images"]        = (20,  TimeSpan.FromMinutes(1)),  // 20 per minute
        ["/api/chatbot"]       = (30,  TimeSpan.FromMinutes(1)),  // 30 per minute
        ["/api/voice"]         = (10,  TimeSpan.FromMinutes(1)),  // 10 per minute
    };

    // Default limits
    private const int DefaultLimit      = 200;
    private static readonly TimeSpan DefaultWindow = TimeSpan.FromMinutes(1);

    // Paths that skip rate limiting
    private static readonly HashSet<string> ExcludedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/health", "/swagger", "/hangfire", "/metrics"
    };

    public RateLimitingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitingMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Skip excluded paths
        if (ExcludedPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        // Build rate limit key: prefer user ID, fall back to IP
        var userId = context.User?.FindFirst(ClaimTypeNames.UserId)?.Value;
        var ip     = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var key    = $"{(userId ?? ip)}:{path}";

        // Get limit for this endpoint
        var (limit, window) = GetLimitForPath(path);

        if (IsRateLimited(key, limit, window))
        {
            _logger.LogWarning(
                "Rate limit exceeded: key={Key}, limit={Limit}/{Window}s",
                key, limit, window.TotalSeconds);

            context.Response.StatusCode  = StatusCodes.Status429TooManyRequests;
            context.Response.ContentType = "application/json";
            context.Response.Headers["Retry-After"] = window.TotalSeconds.ToString();
            context.Response.Headers["X-RateLimit-Limit"] = limit.ToString();
            context.Response.Headers["X-RateLimit-Window"] = window.TotalSeconds.ToString();

            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                message = $"Rate limit exceeded. Maximum {limit} requests per " +
                          $"{FormatWindow(window)}. Please try again later.",
                retryAfterSeconds = (int)window.TotalSeconds
            });
            return;
        }

        // Add rate limit headers to all responses
        context.Response.Headers["X-RateLimit-Limit"]     = limit.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] =
            Math.Max(0, limit - GetRequestCount(key, window)).ToString();

        await _next(context);
    }

    private static bool IsRateLimited(string key, int limit, TimeSpan window)
    {
        var now    = DateTime.UtcNow;
        var cutoff = now - window;

        lock (_lock)
        {
            if (!_requests.ContainsKey(key))
                _requests[key] = new List<DateTime>();

            var timestamps = _requests[key];

            // Remove expired timestamps
            timestamps.RemoveAll(t => t < cutoff);

            if (timestamps.Count >= limit)
                return true;

            timestamps.Add(now);
            return false;
        }
    }

    private static int GetRequestCount(string key, TimeSpan window)
    {
        var cutoff = DateTime.UtcNow - window;
        lock (_lock)
        {
            if (!_requests.TryGetValue(key, out var timestamps))
                return 0;
            return timestamps.Count(t => t >= cutoff);
        }
    }

    private static (int Limit, TimeSpan Window) GetLimitForPath(string path)
    {
        foreach (var (endpoint, limits) in EndpointLimits)
        {
            if (path.StartsWith(endpoint, StringComparison.OrdinalIgnoreCase))
                return limits;
        }
        return (DefaultLimit, DefaultWindow);
    }

    private static string FormatWindow(TimeSpan window) =>
        window.TotalHours >= 1
            ? $"{window.TotalHours:F0} hour(s)"
            : $"{window.TotalMinutes:F0} minute(s)";
}
