using MAPS.API.Services.Admin;

namespace MAPS.API.Controllers;

[ApiController]
[Route("api/analytics")]
[Authorize(Policy = PolicyNames.AdminOnly)]
public class AnalyticsController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AnalyticsController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    /// <summary>GET /api/analytics/dashboard</summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var result = await _adminService.GetDashboardStatsAsync();
        return Ok(result);
    }
}
