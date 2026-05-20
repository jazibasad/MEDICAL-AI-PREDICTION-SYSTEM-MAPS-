using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MAPS.Web.Controllers;

[Authorize(Roles = "Patient")]
[Route("patient")]
public class PatientWebController : Controller
{
    [HttpGet("dashboard")]
    public IActionResult Dashboard() => View();

    [HttpGet("appointments")]
    public IActionResult Appointments() => View();

    [HttpGet("appointments/book")]
    public IActionResult BookAppointment() => View();

    [HttpGet("health")]
    public IActionResult HealthSummary() => View();

    [HttpGet("feedback")]
    public IActionResult Feedback() => View();
}
