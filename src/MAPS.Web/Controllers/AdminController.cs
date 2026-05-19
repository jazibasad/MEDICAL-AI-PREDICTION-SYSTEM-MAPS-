using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace MAPS.Web.Controllers;

[Authorize(Roles = "Admin")]
[Route("admin")]
public class AdminController : Controller
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration     _config;

    public AdminController(IHttpClientFactory httpFactory, IConfiguration config)
    {
        _httpFactory = httpFactory;
        _config      = config;
    }

    // GET /admin/dashboard
    [HttpGet("dashboard")]
    public IActionResult Dashboard() => View();

    // GET /admin/users
    [HttpGet("users")]
    public IActionResult Users() => View();

    // GET /admin/users/{id}
    [HttpGet("users/{id:guid}")]
    public IActionResult UserDetail(Guid id)
    {
        ViewBag.UserId = id;
        return View();
    }

    // GET /admin/assignments
    [HttpGet("assignments")]
    public IActionResult Assignments() => View();

    // GET /admin/pending
    [HttpGet("pending")]
    public IActionResult Pending() => View();

    // GET /admin/audit
    [HttpGet("audit")]
    public IActionResult AuditLog() => View();
}
