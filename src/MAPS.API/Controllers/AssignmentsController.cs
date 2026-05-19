using MAPS.API.Services.Admin;
using MAPS.Shared.DTOs.Common;

namespace MAPS.API.Controllers;

[ApiController]
[Route("api/assignments")]
[Authorize(Policy = PolicyNames.AdminOnly)]
public class AssignmentsController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AssignmentsController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    private Guid AdminId => Guid.Parse(
        User.FindFirst(ClaimTypeNames.UserId)!.Value);

    /// <summary>GET /api/assignments/doctors — all active doctors with patient counts</summary>
    [HttpGet("doctors")]
    public async Task<IActionResult> GetDoctors()
    {
        var result = await _adminService.GetAllDoctorsAsync();
        return Ok(result);
    }

    /// <summary>GET /api/assignments/unassigned — patients with no doctor</summary>
    [HttpGet("unassigned")]
    public async Task<IActionResult> GetUnassigned()
    {
        var result = await _adminService.GetUnassignedPatientsAsync();
        return Ok(result);
    }

    /// <summary>POST /api/assignments — assign patient to doctor</summary>
    [HttpPost]
    public async Task<IActionResult> Assign([FromBody] AssignRequest req)
    {
        var result = await _adminService.AssignPatientToDoctorAsync(
            req.DoctorId, req.PatientId, AdminId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>DELETE /api/assignments/{id} — remove assignment</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Unassign(Guid id)
    {
        var result = await _adminService.UnassignPatientAsync(id, AdminId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>PUT /api/assignments/transfer — transfer patient to new doctor</summary>
    [HttpPut("transfer")]
    public async Task<IActionResult> Transfer([FromBody] TransferRequest req)
    {
        var result = await _adminService.TransferPatientAsync(
            req.PatientId, req.NewDoctorId, AdminId);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

public record AssignRequest(Guid DoctorId, Guid PatientId);
public record TransferRequest(Guid PatientId, Guid NewDoctorId);
