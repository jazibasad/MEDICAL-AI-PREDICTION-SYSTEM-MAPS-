using MAPS.API.Services.Reports;
using MAPS.Shared.DTOs.Common;

namespace MAPS.API.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Policy = PolicyNames.DoctorOrAdmin)]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    private Guid DoctorId => Guid.Parse(
        User.FindFirst(MAPS.Shared.Constants.ClaimTypeNames.UserId)!.Value);

    /// <summary>POST /api/reports/patient-summary/{patientId}</summary>
    [HttpPost("patient-summary/{patientId:guid}")]
    public async Task<IActionResult> GeneratePatientSummary(Guid patientId)
    {
        var result = await _reportService.GeneratePatientSummaryAsync(patientId, DoctorId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>POST /api/reports/prediction/{predictionId}</summary>
    [HttpPost("prediction/{predictionId:guid}")]
    public async Task<IActionResult> GeneratePredictionReport(Guid predictionId)
    {
        var result = await _reportService.GeneratePredictionReportAsync(predictionId, DoctorId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>POST /api/reports/consultation/{patientId}</summary>
    [HttpPost("consultation/{patientId:guid}")]
    public async Task<IActionResult> GenerateConsultationReport(
        Guid patientId, [FromBody] ConsultationReportRequest req)
    {
        var result = await _reportService.GenerateConsultationReportAsync(
            patientId, DoctorId, req.Notes);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

public record ConsultationReportRequest(string Notes);
