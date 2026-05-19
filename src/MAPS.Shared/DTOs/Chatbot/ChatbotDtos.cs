using MAPS.Shared.Enums;

namespace MAPS.Shared.DTOs.Chatbot;

public class ChatbotQueryRequest
{
    public Guid SessionId { get; set; }
    public Guid? PatientContextId { get; set; }  // Optional patient context for RAG
    public InputModality Modality { get; set; }

    // Text input
    public string? TextQuery { get; set; }

    // Image input — MinIO key
    public string? ImageKey { get; set; }

    // Voice input — MinIO key of audio file
    public string? AudioKey { get; set; }

    // Document input — MinIO key of PDF/DOCX
    public string? DocumentKey { get; set; }
}

public class ChatbotResponse
{
    public Guid MessageId { get; set; }
    public Guid SessionId { get; set; }
    public string ResponseText { get; set; } = string.Empty;
    public string Disclaimer { get; set; } = "⚠️ AI suggestion for clinical decision support only. Validate with clinical judgment.";
    public double ContextConfidence { get; set; }
    public List<string> SourceCitations { get; set; } = new();
    public bool WasRedirectedToProtocol { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

public class ChatbotSessionDto
{
    public Guid SessionId { get; set; }
    public Guid DoctorId { get; set; }
    public Guid? PatientContextId { get; set; }
    public string ContextSummary { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public List<ChatbotMessageSummary> Messages { get; set; } = new();
}

public class ChatbotMessageSummary
{
    public Guid MessageId { get; set; }
    public InputModality Modality { get; set; }
    public string Role { get; set; } = string.Empty; // "user" or "assistant"
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class LiteratureSearchRequest
{
    public string Query { get; set; } = string.Empty;
    public int TopK { get; set; } = 5;
}

public class LiteratureSearchResult
{
    public string Title { get; set; } = string.Empty;
    public string Passage { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public double SimilarityScore { get; set; }
}
