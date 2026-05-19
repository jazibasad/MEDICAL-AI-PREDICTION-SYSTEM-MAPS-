using MAPS.Shared.Enums;

namespace MAPS.Shared.DTOs.Prediction;

public class PredictionRequest
{
    public Guid PatientId { get; set; }
    public DiseaseType DiseaseType { get; set; }
    public InputModality InputModality { get; set; }

    // Structured input (Diabetes, Heart Disease)
    public Dictionary<string, double>? StructuredFeatures { get; set; }

    // Text prompt input (Ollama)
    public string? TextPrompt { get; set; }

    // Image input — base64 or MinIO object key
    public string? ImageKey { get; set; }
}

public class PredictionResultDto
{
    public Guid PredictionId { get; set; }
    public Guid PatientId { get; set; }
    public Guid DoctorId { get; set; }
    public DiseaseType DiseaseType { get; set; }
    public InputModality InputModality { get; set; }
    public string PrimaryDiagnosis { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public PredictionStatus Status { get; set; }
    public bool IsSharedWithPatient { get; set; }
    public string? DoctorInterpretation { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class DifferentialDiagnosisRequest
{
    public Guid PatientId { get; set; }
    public List<string> Symptoms { get; set; } = new();
    public string? AdditionalContext { get; set; }
}

public class DifferentialDiagnosisDto
{
    public Guid DdxId { get; set; }
    public Guid PredictionId { get; set; }
    public int RankPosition { get; set; }
    public string Condition { get; set; } = string.Empty;
    public double Probability { get; set; }
    public string ReasoningChain { get; set; } = string.Empty;
    public List<string> ConfirmatoryTests { get; set; } = new();
}

public class SharePredictionRequest
{
    public Guid PredictionId { get; set; }
    public string DoctorInterpretation { get; set; } = string.Empty;
    public List<string> RecommendedNextSteps { get; set; } = new();
}
