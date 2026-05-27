using Microsoft.EntityFrameworkCore;
using MAPS.API.Data;
using MAPS.API.Data.Entities;
using MAPS.API.Services.Prediction;
using MAPS.API.Services.Storage;
using MAPS.API.Services.Voice;
using MAPS.ML.ImageAnalysis;
using MAPS.Shared.DTOs.Chatbot;
using MAPS.Shared.DTOs.Common;
using MAPS.Shared.Enums;

namespace MAPS.API.Services.Chatbot;

public interface IChatbotOrchestrator
{
    Task<ApiResponse<ChatbotResponse>>    QueryAsync(
        ChatbotQueryRequest req, Guid doctorId);
    Task<ApiResponse<ChatbotSessionDto>>  StartSessionAsync(
        Guid doctorId, Guid? patientContextId);
    Task<ApiResponse<ChatbotSessionDto>>  GetHistoryAsync(
        Guid sessionId, Guid doctorId);
    Task<ApiResponse<List<ChatbotSessionDto>>> GetSessionsAsync(Guid doctorId);
}

public class ChatbotOrchestrator : IChatbotOrchestrator
{
    private readonly AppDbContext              _context;
    private readonly IRagContextBuilder        _ragBuilder;
    private readonly IOllamaService            _ollama;
    private readonly ISafetyGuardModule        _safetyGuard;
    private readonly IWhisperTranscriptionService _whisper;
    private readonly IMinioStorageService      _storage;
    private readonly ILogger<ChatbotOrchestrator> _logger;

    private const string SystemPrompt = """
        You are MAPS Clinical AI Assistant, a specialized medical decision support system
        for qualified doctors. You help with:
        - Clinical reasoning and differential diagnosis support
        - Medication information and drug interactions
        - Medical literature and clinical guidelines
        - Patient-specific analysis based on provided health records

        STRICT RULES:
        1. Only provide clinical decision SUPPORT — never make final diagnoses
        2. Always recommend clinical validation of your suggestions
        3. Never provide direct prescriptions — only medication information
        4. In emergencies, redirect to established protocols immediately
        5. Base responses on provided patient context when available
        6. Clearly distinguish between evidence-based information and suggestions

        You are only accessible to licensed medical doctors. Respond with clinical
        precision, appropriate medical terminology, and evidence-based reasoning.
        """;

    public ChatbotOrchestrator(
        AppDbContext                  context,
        IRagContextBuilder            ragBuilder,
        IOllamaService                ollama,
        ISafetyGuardModule            safetyGuard,
        IWhisperTranscriptionService  whisper,
        IMinioStorageService          storage,
        ILogger<ChatbotOrchestrator>  logger)
    {
        _context     = context;
        _ragBuilder  = ragBuilder;
        _ollama      = ollama;
        _safetyGuard = safetyGuard;
        _whisper     = whisper;
        _storage     = storage;
        _logger      = logger;
    }

    // ── Start a new chat session ──────────────────────────────────────────────
    public async Task<ApiResponse<ChatbotSessionDto>> StartSessionAsync(
        Guid doctorId, Guid? patientContextId)
    {
        var session = new ChatSession
        {
            DoctorId         = doctorId,
            PatientContextId = patientContextId,
            StartedAt        = DateTime.UtcNow,
            ContextSummary   = patientContextId.HasValue
                ? "Session with patient context"
                : "General clinical query session"
        };

        _context.ChatSessions.Add(session);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Chatbot session started: {SessionId} for doctor {DoctorId}, patient={PatientId}",
            session.SessionId, doctorId, patientContextId);

        return ApiResponse<ChatbotSessionDto>.Ok(new ChatbotSessionDto
        {
            SessionId        = session.SessionId,
            DoctorId         = doctorId,
            PatientContextId = patientContextId,
            ContextSummary   = session.ContextSummary,
            StartedAt        = session.StartedAt,
            Messages         = new List<ChatbotMessageSummary>()
        });
    }

    // ── Main query handler — routes by modality ────────────────────────────────
    public async Task<ApiResponse<ChatbotResponse>> QueryAsync(
        ChatbotQueryRequest req, Guid doctorId)
    {
        // Validate session belongs to doctor
        var session = await _context.ChatSessions
            .FirstOrDefaultAsync(s => s.SessionId == req.SessionId &&
                                       s.DoctorId  == doctorId);
        if (session is null)
            return ApiResponse<ChatbotResponse>.Fail(
                "Session not found. Please start a new session.");

        // ── Step 1: Route input by modality ───────────────────────────────────
        string userText;
        string? attachmentUrl = null;

        try
        {
            (userText, attachmentUrl) = req.Modality switch
            {
                InputModality.Text     => (req.TextQuery ?? "", null),
                InputModality.Voice    => await HandleVoiceInputAsync(req.AudioKey),
                InputModality.Image    => await HandleImageInputAsync(req.ImageKey, doctorId),
                InputModality.Document => await HandleDocumentInputAsync(req.DocumentKey),
                _                      => (req.TextQuery ?? "", null)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Input routing failed for modality {Modality}", req.Modality);
            return ApiResponse<ChatbotResponse>.Fail(
                $"Failed to process {req.Modality} input: {ex.Message}");
        }

        if (string.IsNullOrWhiteSpace(userText))
            return ApiResponse<ChatbotResponse>.Fail("No query content could be extracted.");

        // ── Step 2: Build RAG context ─────────────────────────────────────────
        var ragContext = await _ragBuilder.BuildContextAsync(
            doctorId, session.PatientContextId, userText, req.SessionId);

        // ── Step 3: Build enriched prompt ─────────────────────────────────────
        var enrichedPrompt = $"""
            {ragContext.ContextPrompt}

            ## Doctor's Query
            {userText}
            """;

        // ── Step 4: Generate Ollama response ──────────────────────────────────
        string rawResponse;
        try
        {
            rawResponse = await _ollama.GenerateResponseAsync(SystemPrompt, enrichedPrompt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ollama generation failed");
            rawResponse = "The AI service is temporarily unavailable. " +
                          "Please consult clinical references directly.";
        }

        // ── Step 5: Apply safety guard ────────────────────────────────────────
        var safeResult = _safetyGuard.Apply(rawResponse, ragContext.ContextConfidence);

        var finalResponse = safeResult.FilteredResponse + safeResult.Disclaimer;

        // ── Step 6: Persist messages ──────────────────────────────────────────
        _context.ChatbotMessages.AddRange(
            new ChatbotMessage
            {
                SessionId     = req.SessionId,
                Role          = "user",
                Modality      = req.Modality,
                Content       = userText,
                AttachmentUrl = attachmentUrl,
                Timestamp     = DateTime.UtcNow
            },
            new ChatbotMessage
            {
                SessionId  = req.SessionId,
                Role       = "assistant",
                Modality   = InputModality.Text,
                Content    = finalResponse,
                AiResponse = rawResponse,
                Timestamp  = DateTime.UtcNow
            });

        await _context.SaveChangesAsync();

        // ── Step 7: Store embedding in ChatbotMemory for future RAG ───────────
        _ = Task.Run(async () =>
        {
            try
            {
                var embedding = await _ollama.GenerateEmbeddingAsync(userText);
                if (embedding.Length > 0)
                {
                    var embStr = "[" + string.Join(",", embedding.Take(1536)) + "]";
                    _context.ChatbotMemories.Add(new ChatbotMemory
                    {
                        DoctorId    = doctorId,
                        SessionId   = req.SessionId,
                        Embedding   = embStr,
                        ContextType = "query",
                        SourceRef   = userText[..Math.Min(userText.Length, 200)]
                    });
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to store chatbot memory embedding");
            }
        });

        _logger.LogInformation(
            "Chatbot query processed: modality={Modality}, session={Session}, " +
            "redirected={Redirected}, prescriptionBlocked={PxBlocked}",
            req.Modality, req.SessionId,
            safeResult.WasRedirectedToProtocol,
            safeResult.PrescriptionBlocked);

        return ApiResponse<ChatbotResponse>.Ok(new ChatbotResponse
        {
            MessageId                = Guid.NewGuid(),
            SessionId                = req.SessionId,
            ResponseText             = finalResponse,
            Disclaimer               = safeResult.Disclaimer,
            ContextConfidence        = ragContext.ContextConfidence,
            SourceCitations          = ragContext.Sources,
            WasRedirectedToProtocol  = safeResult.WasRedirectedToProtocol,
            GeneratedAt              = DateTime.UtcNow
        });
    }

    // ── Voice Input Handler ───────────────────────────────────────────────────
    private async Task<(string text, string? url)> HandleVoiceInputAsync(string? audioKey)
    {
        if (string.IsNullOrEmpty(audioKey))
            throw new ArgumentException("Audio key is required for voice input.");

        var stream = await _storage.DownloadAsync(audioKey);
        var result = await _whisper.TranscribeAsync(stream, "audio.webm", "audio/webm");

        if (!result.Success)
            throw new InvalidOperationException(result.Message);

        return (result.Data!.Text, audioKey);
    }

    // ── Image Input Handler ───────────────────────────────────────────────────
    private async Task<(string text, string? url)> HandleImageInputAsync(
        string? imageKey, Guid doctorId)
    {
        if (string.IsNullOrEmpty(imageKey))
            throw new ArgumentException("Image key is required for image input.");

        // Generate a description prompt for the image
        // In production: ONNX image analysis → visual description
        var imagePrompt = $"[Medical image uploaded: {imageKey}] " +
                          "Please provide clinical analysis based on the patient context. " +
                          "Note: Direct image analysis via vision model is being processed.";

        var url = await _storage.GetPresignedUrlAsync(imageKey);
        return (imagePrompt, url);
    }

    // ── Document Input Handler ────────────────────────────────────────────────
    private async Task<(string text, string? url)> HandleDocumentInputAsync(string? docKey)
    {
        if (string.IsNullOrEmpty(docKey))
            throw new ArgumentException("Document key is required for document input.");

        // Download and extract text content
        var stream = await _storage.DownloadAsync(docKey);
        using var reader = new System.IO.StreamReader(stream);
        var content = await reader.ReadToEndAsync();

        // Truncate to 3000 chars to fit in context window
        var truncated = content.Length > 3000
            ? content[..3000] + "\n[Document truncated for context window...]"
            : content;

        return ($"[Document content]\n{truncated}", docKey);
    }

    // ── Get Session History ───────────────────────────────────────────────────
    public async Task<ApiResponse<ChatbotSessionDto>> GetHistoryAsync(
        Guid sessionId, Guid doctorId)
    {
        var session = await _context.ChatSessions
            .Include(s => s.Messages)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId &&
                                       s.DoctorId  == doctorId);

        if (session is null)
            return ApiResponse<ChatbotSessionDto>.Fail("Session not found.");

        return ApiResponse<ChatbotSessionDto>.Ok(new ChatbotSessionDto
        {
            SessionId        = session.SessionId,
            DoctorId         = doctorId,
            PatientContextId = session.PatientContextId,
            ContextSummary   = session.ContextSummary,
            StartedAt        = session.StartedAt,
            Messages         = session.Messages
                .OrderBy(m => m.Timestamp)
                .Select(m => new ChatbotMessageSummary
                {
                    MessageId = m.MessageId,
                    Modality  = m.Modality,
                    Role      = m.Role,
                    Content   = m.Content,
                    Timestamp = m.Timestamp
                }).ToList()
        });
    }

    // ── Get All Sessions for Doctor ───────────────────────────────────────────
    public async Task<ApiResponse<List<ChatbotSessionDto>>> GetSessionsAsync(Guid doctorId)
    {
        var sessions = await _context.ChatSessions
            .Where(s => s.DoctorId == doctorId)
            .OrderByDescending(s => s.StartedAt)
            .Take(20)
            .Select(s => new ChatbotSessionDto
            {
                SessionId        = s.SessionId,
                DoctorId         = doctorId,
                PatientContextId = s.PatientContextId,
                ContextSummary   = s.ContextSummary,
                StartedAt        = s.StartedAt,
                Messages         = new List<ChatbotMessageSummary>()
            })
            .ToListAsync();

        return ApiResponse<List<ChatbotSessionDto>>.Ok(sessions);
    }
}
