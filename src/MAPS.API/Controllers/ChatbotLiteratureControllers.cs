using MAPS.API.Services.Chatbot;
using MAPS.API.Services.Literature;
using MAPS.Shared.DTOs.Chatbot;
using MAPS.Shared.DTOs.Common;

namespace MAPS.API.Controllers;

// ─── Chatbot Controller (Doctor Only) ─────────────────────────────────────────
[ApiController]
[Route("api/chatbot")]
[Authorize(Policy = PolicyNames.DoctorOnly)]
public class ChatbotController : ControllerBase
{
    private readonly IChatbotOrchestrator _orchestrator;

    public ChatbotController(IChatbotOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    private Guid DoctorId => Guid.Parse(
        User.FindFirst(ClaimTypeNames.UserId)!.Value);

    /// <summary>POST /api/chatbot/session — start new session</summary>
    [HttpPost("session")]
    public async Task<IActionResult> StartSession(
        [FromBody] StartSessionRequest req)
    {
        var result = await _orchestrator.StartSessionAsync(
            DoctorId, req.PatientContextId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>POST /api/chatbot/query — send query to AI assistant</summary>
    [HttpPost("query")]
    public async Task<IActionResult> Query([FromBody] ChatbotQueryRequest req)
    {
        var result = await _orchestrator.QueryAsync(req, DoctorId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>GET /api/chatbot/history/{sessionId}</summary>
    [HttpGet("history/{sessionId:guid}")]
    public async Task<IActionResult> GetHistory(Guid sessionId)
    {
        var result = await _orchestrator.GetHistoryAsync(sessionId, DoctorId);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>GET /api/chatbot/sessions — all sessions for doctor</summary>
    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions()
    {
        var result = await _orchestrator.GetSessionsAsync(DoctorId);
        return Ok(result);
    }
}

// ─── Literature Search Controller (Doctor Only) ───────────────────────────────
[ApiController]
[Route("api/literature")]
[Authorize(Policy = PolicyNames.DoctorOnly)]
public class LiteratureController : ControllerBase
{
    private readonly ILiteratureSearchService _literatureService;

    public LiteratureController(ILiteratureSearchService literatureService)
    {
        _literatureService = literatureService;
    }

    private Guid DoctorId => Guid.Parse(
        User.FindFirst(ClaimTypeNames.UserId)!.Value);

    /// <summary>POST /api/literature/search</summary>
    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] LiteratureSearchRequest req)
    {
        var result = await _literatureService.SearchAsync(req, DoctorId);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

public record StartSessionRequest(Guid? PatientContextId);
