using MAPS.API.Services.Doctor;
using MAPS.Shared.DTOs.Common;

namespace MAPS.API.Controllers;

// ─── Patients Controller ──────────────────────────────────────────────────────
[ApiController]
[Route("api/patients")]
[Authorize(Policy = PolicyNames.DoctorOrAdmin)]
public class PatientsController : ControllerBase
{
    private readonly IDoctorService _doctorService;

    public PatientsController(IDoctorService doctorService)
    {
        _doctorService = doctorService;
    }

    private Guid DoctorId => Guid.Parse(
        User.FindFirst(ClaimTypeNames.UserId)!.Value);

    /// <summary>GET /api/patients/{id}/timeline — full patient history</summary>
    [HttpGet("{id:guid}/timeline")]
    public async Task<IActionResult> GetTimeline(Guid id)
    {
        var result = await _doctorService.GetPatientTimelineAsync(id, DoctorId);
        return result.Success ? Ok(result) : Forbid();
    }

    /// <summary>GET /api/patients/queue — doctor's patient queue sorted by risk</summary>
    [HttpGet("queue")]
    [Authorize(Policy = PolicyNames.DoctorOnly)]
    public async Task<IActionResult> GetQueue()
    {
        var result = await _doctorService.GetPatientQueueAsync(DoctorId);
        return Ok(result);
    }
}

// ─── Prescriptions Controller ─────────────────────────────────────────────────
[ApiController]
[Route("api/prescriptions")]
[Authorize(Policy = PolicyNames.DoctorOrAdmin)]
public class PrescriptionsController : ControllerBase
{
    private readonly IDoctorService _doctorService;

    public PrescriptionsController(IDoctorService doctorService)
    {
        _doctorService = doctorService;
    }

    private Guid DoctorId => Guid.Parse(
        User.FindFirst(ClaimTypeNames.UserId)!.Value);

    /// <summary>GET /api/prescriptions/patient/{id}</summary>
    [HttpGet("patient/{id:guid}")]
    public async Task<IActionResult> GetByPatient(Guid id)
    {
        var result = await _doctorService.GetPrescriptionsAsync(id, DoctorId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>POST /api/prescriptions</summary>
    [HttpPost]
    [Authorize(Policy = PolicyNames.DoctorOnly)]
    public async Task<IActionResult> Create([FromBody] CreatePrescriptionRequest req)
    {
        var result = await _doctorService.CreatePrescriptionAsync(req, DoctorId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>PUT /api/prescriptions/{id}/status</summary>
    [HttpPut("{id:guid}/status")]
    [Authorize(Policy = PolicyNames.DoctorOnly)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusRequest req)
    {
        var result = await _doctorService.UpdatePrescriptionStatusAsync(id, req.Status, DoctorId);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

// ─── Doctor Dashboard Controller ──────────────────────────────────────────────
[ApiController]
[Route("api/doctor")]
[Authorize(Policy = PolicyNames.DoctorOnly)]
public class DoctorController : ControllerBase
{
    private readonly IDoctorService _doctorService;

    public DoctorController(IDoctorService doctorService)
    {
        _doctorService = doctorService;
    }

    private Guid DoctorId => Guid.Parse(
        User.FindFirst(ClaimTypeNames.UserId)!.Value);

    /// <summary>GET /api/doctor/dashboard</summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var result = await _doctorService.GetDashboardAsync(DoctorId);
        return Ok(result);
    }
}

// ─── Drugs Controller ─────────────────────────────────────────────────────────
[ApiController]
[Route("api/drugs")]
[Authorize(Policy = PolicyNames.DoctorOnly)]
public class DrugsController : ControllerBase
{
    private readonly IDoctorService _doctorService;

    public DrugsController(IDoctorService doctorService)
    {
        _doctorService = doctorService;
    }

    /// <summary>POST /api/drugs/check-interactions</summary>
    [HttpPost("check-interactions")]
    public async Task<IActionResult> CheckInteractions(
        [FromBody] DrugCheckRequest req)
    {
        var result = await _doctorService.CheckDrugInteractionsAsync(
            req.PatientId, req.NewMedications);
        return Ok(result);
    }
}

public record UpdateStatusRequest(string Status);
public record DrugCheckRequest(Guid PatientId, string NewMedications);
