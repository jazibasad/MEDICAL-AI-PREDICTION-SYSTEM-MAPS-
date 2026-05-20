using MAPS.API.Services.Patient;
using MAPS.Shared.DTOs.Common;

namespace MAPS.API.Controllers;

// ─── Appointments Controller ──────────────────────────────────────────────────
[ApiController]
[Route("api/appointments")]
[Authorize]
public class AppointmentsController : ControllerBase
{
    private readonly IPatientService _patientService;

    public AppointmentsController(IPatientService patientService)
    {
        _patientService = patientService;
    }

    private Guid UserId => Guid.Parse(
        User.FindFirst(ClaimTypeNames.UserId)!.Value);

    /// <summary>GET /api/appointments — patient's appointments</summary>
    [HttpGet]
    [Authorize(Policy = PolicyNames.PatientOnly)]
    public async Task<IActionResult> GetAll()
    {
        var result = await _patientService.GetAppointmentsAsync(UserId);
        return Ok(result);
    }

    /// <summary>GET /api/appointments/slots — available time slots</summary>
    [HttpGet("slots")]
    [Authorize(Policy = PolicyNames.PatientOnly)]
    public async Task<IActionResult> GetSlots()
    {
        var result = await _patientService.GetAvailableSlotsAsync(UserId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>POST /api/appointments — book new appointment</summary>
    [HttpPost]
    [Authorize(Policy = PolicyNames.PatientOnly)]
    public async Task<IActionResult> Book([FromBody] BookAppointmentRequest req)
    {
        var result = await _patientService.BookAppointmentAsync(UserId, req);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>PUT /api/appointments/{id}/cancel</summary>
    [HttpPut("{id:guid}/cancel")]
    [Authorize(Policy = PolicyNames.PatientOnly)]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var result = await _patientService.CancelAppointmentAsync(id, UserId);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

// ─── Patient API Controller ───────────────────────────────────────────────────
[ApiController]
[Route("api/patient")]
[Authorize(Policy = PolicyNames.PatientOnly)]
public class PatientApiController : ControllerBase
{
    private readonly IPatientService _patientService;

    public PatientApiController(IPatientService patientService)
    {
        _patientService = patientService;
    }

    private Guid UserId => Guid.Parse(
        User.FindFirst(ClaimTypeNames.UserId)!.Value);

    /// <summary>GET /api/patient/dashboard</summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var result = await _patientService.GetDashboardAsync(UserId);
        return Ok(result);
    }

    /// <summary>GET /api/patient/health-summary</summary>
    [HttpGet("health-summary")]
    public async Task<IActionResult> HealthSummary()
    {
        var result = await _patientService.GetHealthSummaryAsync(UserId);
        return Ok(result);
    }

    /// <summary>POST /api/patient/feedback</summary>
    [HttpPost("feedback")]
    public async Task<IActionResult> SubmitFeedback([FromBody] SubmitFeedbackRequest req)
    {
        var result = await _patientService.SubmitFeedbackAsync(UserId, req);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

// ─── Feedback Controller (Admin view) ────────────────────────────────────────
[ApiController]
[Route("api/feedback")]
public class FeedbackController : ControllerBase
{
    private readonly MAPS.API.Data.AppDbContext _context;

    public FeedbackController(MAPS.API.Data.AppDbContext context)
    {
        _context = context;
    }

    /// <summary>GET /api/feedback — admin only, all feedback</summary>
    [HttpGet]
    [Authorize(Policy = PolicyNames.AdminOnly)]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var feedback = await _context.Feedbacks
            .Include(f => f.Patient).ThenInclude(p => p.User)
            .OrderByDescending(f => f.SubmittedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new
            {
                f.FeedbackId,
                PatientName    = f.Patient.User.FullName,
                f.DoctorId,
                f.Rating,
                f.Comment,
                f.SentimentLabel,
                f.SentimentScore,
                f.SubmittedAt
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(feedback));
    }
}
