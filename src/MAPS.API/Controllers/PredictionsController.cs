using MAPS.API.Services.Prediction;
using MAPS.Shared.DTOs.Prediction;
using MAPS.Shared.DTOs.Common;

namespace MAPS.API.Controllers;

[ApiController]
[Route("api/predictions")]
[Authorize(Policy = PolicyNames.DoctorOrAdmin)]
public class PredictionsController : ControllerBase
{
    private readonly IPredictionService _predictionService;

    public PredictionsController(IPredictionService predictionService)
    {
        _predictionService = predictionService;
    }

    private Guid DoctorId => Guid.Parse(
        User.FindFirst(ClaimTypeNames.UserId)!.Value);

    /// <summary>POST /api/predictions — run new prediction</summary>
    [HttpPost]
    [Authorize(Policy = PolicyNames.DoctorOnly)]
    public async Task<IActionResult> Create([FromBody] PredictionRequest req)
    {
        var result = await _predictionService.PredictAsync(req, DoctorId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>GET /api/predictions/{id}</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _predictionService.GetByIdAsync(id, DoctorId);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>GET /api/predictions/patient/{patientId}</summary>
    [HttpGet("patient/{patientId:guid}")]
    public async Task<IActionResult> GetByPatient(Guid patientId)
    {
        var result = await _predictionService.GetByPatientAsync(patientId, DoctorId);
        return Ok(result);
    }

    /// <summary>POST /api/predictions/differential — differential diagnosis</summary>
    [HttpPost("differential")]
    [Authorize(Policy = PolicyNames.DoctorOnly)]
    public async Task<IActionResult> GetDifferential(
        [FromBody] DifferentialDiagnosisRequest req)
    {
        var result = await _predictionService.GetDifferentialAsync(req, DoctorId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>POST /api/predictions/share — share with patient</summary>
    [HttpPost("share")]
    [Authorize(Policy = PolicyNames.DoctorOnly)]
    public async Task<IActionResult> Share([FromBody] SharePredictionRequest req)
    {
        var result = await _predictionService.ShareWithPatientAsync(req, DoctorId);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
