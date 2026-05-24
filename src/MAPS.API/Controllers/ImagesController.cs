using MAPS.API.Services.ImageAnalysis;
using MAPS.Shared.DTOs.Common;
using MAPS.Shared.Enums;

namespace MAPS.API.Controllers;

[ApiController]
[Route("api/images")]
[Authorize(Policy = PolicyNames.DoctorOnly)]
public class ImagesController : ControllerBase
{
    private readonly IImageAnalysisService _imageService;

    public ImagesController(IImageAnalysisService imageService)
    {
        _imageService = imageService;
    }

    private Guid DoctorId => Guid.Parse(
        User.FindFirst(ClaimTypeNames.UserId)!.Value);

    /// <summary>
    /// POST /api/images/analyse
    /// Multipart form: file + patientId + disease
    /// </summary>
    [HttpPost("analyse")]
    [RequestSizeLimit(25_000_000)] // 25MB
    public async Task<IActionResult> Analyse(
        [FromForm] IFormFile   file,
        [FromForm] Guid        patientId,
        [FromForm] DiseaseType disease)
    {
        var result = await _imageService.AnalyseAsync(
            file, disease, patientId, DoctorId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>GET /api/images/{id}</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _imageService.GetByIdAsync(id, DoctorId);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>GET /api/images/patient/{patientId}</summary>
    [HttpGet("patient/{patientId:guid}")]
    public async Task<IActionResult> GetByPatient(Guid patientId)
    {
        var result = await _imageService.GetByPatientAsync(patientId, DoctorId);
        return Ok(result);
    }
}
