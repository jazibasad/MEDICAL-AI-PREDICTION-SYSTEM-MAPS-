using MAPS.API.Services.Risk;
using MAPS.API.Services.NLP;
using MAPS.API.Services.Voice;
using MAPS.Shared.DTOs.Common;

namespace MAPS.API.Controllers;

// ─── Risk Assessment Controller ───────────────────────────────────────────────
[ApiController]
[Route("api/risks")]
[Authorize(Policy = PolicyNames.DoctorOrAdmin)]
public class RisksController : ControllerBase
{
    private readonly IRiskAssessmentService _riskService;

    public RisksController(IRiskAssessmentService riskService)
    {
        _riskService = riskService;
    }

    private Guid DoctorId => Guid.Parse(
        User.FindFirst(ClaimTypeNames.UserId)!.Value);

    /// <summary>GET /api/risks/patient/{id} — latest risk for patient</summary>
    [HttpGet("patient/{id:guid}")]
    public async Task<IActionResult> GetLatest(Guid id)
    {
        var result = await _riskService.GetLatestAsync(id, DoctorId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>POST /api/risks/patient/{id}/recalculate</summary>
    [HttpPost("patient/{id:guid}/recalculate")]
    [Authorize(Policy = PolicyNames.DoctorOnly)]
    public async Task<IActionResult> Recalculate(Guid id)
    {
        var result = await _riskService.RecalculateAsync(id, DoctorId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>GET /api/risks/alerts — high-risk patients for current doctor</summary>
    [HttpGet("alerts")]
    [Authorize(Policy = PolicyNames.DoctorOnly)]
    public async Task<IActionResult> GetAlerts()
    {
        var result = await _riskService.GetAlertsAsync(DoctorId);
        return Ok(result);
    }
}

// ─── Clinical Notes Controller ────────────────────────────────────────────────
[ApiController]
[Route("api/notes")]
[Authorize(Policy = PolicyNames.DoctorOnly)]
public class NotesController : ControllerBase
{
    private readonly INlpService _nlpService;

    public NotesController(INlpService nlpService)
    {
        _nlpService = nlpService;
    }

    private Guid DoctorId => Guid.Parse(
        User.FindFirst(ClaimTypeNames.UserId)!.Value);

    /// <summary>POST /api/notes — create clinical note with NLP processing</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateNoteRequest req)
    {
        var result = await _nlpService.CreateNoteAsync(req, DoctorId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>GET /api/notes/patient/{patientId}</summary>
    [HttpGet("patient/{patientId:guid}")]
    public async Task<IActionResult> GetByPatient(Guid patientId)
    {
        var result = await _nlpService.GetNotesByPatientAsync(patientId, DoctorId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>GET /api/notes/{id}/entities — extracted entities for a note</summary>
    [HttpGet("{id:guid}/entities")]
    public async Task<IActionResult> GetEntities(Guid id)
    {
        var result = await _nlpService.GetEntitiesAsync(id, DoctorId);
        return result.Success ? Ok(result) : NotFound(result);
    }
}

// ─── Voice Dictation Controller ───────────────────────────────────────────────
[ApiController]
[Route("api/voice")]
[Authorize(Policy = PolicyNames.DoctorOnly)]
public class VoiceController : ControllerBase
{
    private readonly IWhisperTranscriptionService _whisperService;

    public VoiceController(IWhisperTranscriptionService whisperService)
    {
        _whisperService = whisperService;
    }

    /// <summary>
    /// POST /api/voice/transcribe
    /// Accepts audio file → returns corrected transcription text
    /// </summary>
    [HttpPost("transcribe")]
    [RequestSizeLimit(50_000_000)] // 50MB for audio
    public async Task<IActionResult> Transcribe([FromForm] IFormFile audio)
    {
        if (audio is null || audio.Length == 0)
            return BadRequest(ApiResponse.Fail("No audio file provided."));

        var allowedTypes = new[]
        {
            "audio/wav", "audio/mpeg", "audio/mp3",
            "audio/ogg", "audio/webm", "audio/mp4"
        };

        if (!allowedTypes.Contains(audio.ContentType.ToLower()))
            return BadRequest(ApiResponse.Fail(
                "Unsupported audio format. Use WAV, MP3, OGG, or WebM."));

        using var stream = audio.OpenReadStream();
        var result = await _whisperService.TranscribeAsync(
            stream, audio.FileName, audio.ContentType);

        return result.Success ? Ok(result) : StatusCode(500, result);
    }

    /// <summary>GET /api/voice/health — check Whisper service availability</summary>
    [HttpGet("health")]
    public async Task<IActionResult> Health()
    {
        var available = await _whisperService.IsAvailableAsync();
        return Ok(ApiResponse<bool>.Ok(available,
            available ? "Whisper service is available." : "Whisper service is unavailable."));
    }
}
