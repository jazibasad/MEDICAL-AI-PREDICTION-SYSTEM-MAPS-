using Microsoft.EntityFrameworkCore;
using MAPS.API.Data;
using MAPS.API.Data.Entities;
using MAPS.API.Services.Prediction;

namespace MAPS.API.Services.Chatbot;

public class RagContextResult
{
    public string ContextPrompt     { get; set; } = string.Empty;
    public float  ContextConfidence { get; set; }
    public List<string> Sources     { get; set; } = new();
}

public interface IRagContextBuilder
{
    Task<RagContextResult> BuildContextAsync(
        Guid    doctorId,
        Guid?   patientContextId,
        string  query,
        Guid    sessionId);
}

public class RagContextBuilder : IRagContextBuilder
{
    private readonly AppDbContext   _context;
    private readonly IOllamaService _ollama;
    private readonly ILogger<RagContextBuilder> _logger;

    public RagContextBuilder(
        AppDbContext             context,
        IOllamaService           ollama,
        ILogger<RagContextBuilder> logger)
    {
        _context = context;
        _ollama  = ollama;
        _logger  = logger;
    }

    public async Task<RagContextResult> BuildContextAsync(
        Guid  doctorId,
        Guid? patientContextId,
        string query,
        Guid  sessionId)
    {
        var parts   = new List<string>();
        var sources = new List<string>();
        float confidence = 0.5f;

        // ── 1. Patient health record context ─────────────────────────────────
        if (patientContextId.HasValue)
        {
            var patient = await _context.PatientProfiles
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.PatientId == patientContextId.Value);

            if (patient is not null)
            {
                parts.Add($"## Patient Context\nName: {patient.User.FullName}\n" +
                          $"DOB: {patient.DateOfBirth?.ToString("dd MMM yyyy") ?? "Unknown"}\n" +
                          $"Blood Group: {patient.BloodGroup}");
                sources.Add("patient_profile");

                // Recent AI predictions
                var predictions = await _context.AIPredictions
                    .Where(p => p.PatientId == patientContextId.Value)
                    .OrderByDescending(p => p.CreatedAt)
                    .Take(3)
                    .ToListAsync();

                if (predictions.Any())
                {
                    var predSummary = predictions.Select(p =>
                        $"- {p.DiseaseType}: {p.PrimaryDiagnosis} ({p.Confidence:P0} confidence, {p.CreatedAt:dd MMM})");
                    parts.Add($"## Recent AI Predictions\n{string.Join("\n", predSummary)}");
                    sources.Add("ai_predictions");
                    confidence = Math.Max(confidence, 0.75f);
                }

                // Active prescriptions
                var prescriptions = await _context.Prescriptions
                    .Where(p => p.PatientId == patientContextId.Value && p.Status == "Active")
                    .Take(5)
                    .ToListAsync();

                if (prescriptions.Any())
                {
                    parts.Add($"## Active Prescriptions\n" +
                              $"{string.Join("\n", prescriptions.Select(p => $"- {p.Medications}"))}");
                    sources.Add("prescriptions");
                }

                // Latest risk score
                var risk = await _context.RiskAssessments
                    .Where(r => r.PatientId == patientContextId.Value)
                    .OrderByDescending(r => r.CalculatedAt)
                    .FirstOrDefaultAsync();

                if (risk is not null)
                {
                    parts.Add($"## Risk Assessment\n" +
                              $"Score: {risk.RiskScore:F1}/100 | Tier: {risk.UrgencyTier} | " +
                              $"Trend: {risk.TrendDirection}");
                    sources.Add("risk_assessment");
                }

                // Recent clinical notes summary
                var notes = await _context.ClinicalNotes
                    .Include(n => n.HealthRecord)
                    .Where(n => n.HealthRecord.PatientId == patientContextId.Value)
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(2)
                    .ToListAsync();

                if (notes.Any())
                {
                    var noteSummaries = notes.Select(n =>
                        $"[{n.CreatedAt:dd MMM}] {n.Summary}");
                    parts.Add($"## Recent Clinical Notes\n{string.Join("\n", noteSummaries)}");
                    sources.Add("clinical_notes");
                    confidence = Math.Max(confidence, 0.80f);
                }
            }
        }

        // ── 2. Conversation history from current session ───────────────────────
        var history = await _context.ChatbotMessages
            .Where(m => m.SessionId == sessionId)
            .OrderByDescending(m => m.Timestamp)
            .Take(6) // Last 3 exchanges
            .OrderBy(m => m.Timestamp)
            .ToListAsync();

        if (history.Any())
        {
            var historyText = history.Select(m =>
                $"{m.Role.ToUpper()}: {m.Content[..Math.Min(m.Content.Length, 200)]}");
            parts.Add($"## Recent Conversation\n{string.Join("\n", historyText)}");
            sources.Add("conversation_history");
        }

        // ── 3. Vector similarity search in ChatbotMemory (pgvector RAG) ───────
        try
        {
            var queryEmbedding = await _ollama.GenerateEmbeddingAsync(query);
            if (queryEmbedding.Length > 0)
            {
                // Raw SQL for pgvector cosine similarity search
                var embeddingStr = "[" + string.Join(",", queryEmbedding.Take(1536)) + "]";
                var similarMemories = await _context.ChatbotMemories
                    .FromSqlRaw(
                        @"SELECT * FROM ""ChatbotMemories""
                          WHERE ""DoctorId"" = {0}
                          ORDER BY ""Embedding"" <=> {1}::vector
                          LIMIT 3",
                        doctorId, embeddingStr)
                    .ToListAsync();

                if (similarMemories.Any())
                {
                    var memText = similarMemories.Select(m =>
                        $"[{m.ContextType}] {m.SourceRef}");
                    parts.Add($"## Related Past Context\n{string.Join("\n", memText)}");
                    sources.Add("vector_memory");
                    confidence = Math.Max(confidence, 0.85f);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "pgvector similarity search failed — continuing without memory");
        }

        var contextPrompt = parts.Count > 0
            ? string.Join("\n\n", parts)
            : "No patient context available for this query.";

        _logger.LogDebug(
            "RAG context built: {Parts} sections, confidence={Conf:F2}, sources={Sources}",
            parts.Count, confidence, string.Join(",", sources));

        return new RagContextResult
        {
            ContextPrompt     = contextPrompt,
            ContextConfidence = confidence,
            Sources           = sources
        };
    }
}
