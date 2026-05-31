using MAPS.API.Services.Analytics;
using MAPS.Shared.DTOs.Common;

namespace MAPS.API.Controllers;

[ApiController]
[Route("api/system")]
[Authorize(Policy = PolicyNames.AdminOnly)]
public class SystemController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;

    public SystemController(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    /// <summary>GET /api/system/containers — Docker container health</summary>
    [HttpGet("containers")]
    public async Task<IActionResult> GetContainers()
    {
        var result = await _analyticsService.GetContainerHealthAsync();
        return Ok(result);
    }

    /// <summary>GET /api/system/health — full system health check</summary>
    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult Health()
    {
        return Ok(ApiResponse<object>.Ok(new
        {
            status    = "healthy",
            timestamp = DateTime.UtcNow,
            version   = "1.0.0",
            service   = "MAPS API"
        }));
    }

    /// <summary>GET /api/system/audit — audit log summary</summary>
    [HttpGet("audit")]
    public async Task<IActionResult> GetAudit([FromQuery] int limit = 50)
    {
        var result = await _analyticsService.GetAuditSummaryAsync(limit);
        return Ok(result);
    }
}

// ─── Extended Analytics Controller ───────────────────────────────────────────
[ApiController]
[Route("api/analytics")]
[Authorize(Policy = PolicyNames.AdminOnly)]
public class FullAnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;

    public FullAnalyticsController(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    /// <summary>GET /api/analytics/dashboard — full system analytics</summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var result = await _analyticsService.GetSystemAnalyticsAsync();
        return Ok(result);
    }

    /// <summary>GET /api/analytics/feedback — feedback analytics</summary>
    [HttpGet("feedback")]
    public async Task<IActionResult> Feedback()
    {
        var result = await _analyticsService.GetFeedbackAnalyticsAsync();
        return Ok(result);
    }

    /// <summary>GET /api/analytics/audit — full audit log</summary>
    [HttpGet("audit")]
    public async Task<IActionResult> Audit([FromQuery] int limit = 100)
    {
        var result = await _analyticsService.GetAuditSummaryAsync(limit);
        return Ok(result);
    }
}
