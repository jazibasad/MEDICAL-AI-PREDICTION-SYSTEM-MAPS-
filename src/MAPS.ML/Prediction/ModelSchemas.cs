using Microsoft.ML.Data;

namespace MAPS.ML.Prediction;

// ─── Diabetes ─────────────────────────────────────────────────────────────────
public class DiabetesInput
{
    [LoadColumn(0)] public float Pregnancies        { get; set; }
    [LoadColumn(1)] public float Glucose            { get; set; }
    [LoadColumn(2)] public float BloodPressure      { get; set; }
    [LoadColumn(3)] public float SkinThickness      { get; set; }
    [LoadColumn(4)] public float Insulin            { get; set; }
    [LoadColumn(5)] public float BMI                { get; set; }
    [LoadColumn(6)] public float DiabetesPedigree   { get; set; }
    [LoadColumn(7)] public float Age                { get; set; }
    [LoadColumn(8)] public bool  Label              { get; set; } // Outcome
}

public class DiabetesPrediction
{
    [ColumnName("PredictedLabel")] public bool   PredictedLabel { get; set; }
    [ColumnName("Probability")]    public float  Probability    { get; set; }
    [ColumnName("Score")]          public float  Score          { get; set; }
}

// ─── Heart Disease ────────────────────────────────────────────────────────────
public class HeartDiseaseInput
{
    [LoadColumn(0)]  public float Age         { get; set; }
    [LoadColumn(1)]  public float Sex         { get; set; }
    [LoadColumn(2)]  public float ChestPain   { get; set; }
    [LoadColumn(3)]  public float RestingBP   { get; set; }
    [LoadColumn(4)]  public float Cholesterol { get; set; }
    [LoadColumn(5)]  public float FastingBS   { get; set; }
    [LoadColumn(6)]  public float RestingECG  { get; set; }
    [LoadColumn(7)]  public float MaxHR       { get; set; }
    [LoadColumn(8)]  public float ExerciseAngina { get; set; }
    [LoadColumn(9)]  public float Oldpeak     { get; set; }
    [LoadColumn(10)] public float STSlope     { get; set; }
    [LoadColumn(11)] public bool  Label       { get; set; } // HeartDisease
}

public class HeartDiseasePrediction
{
    [ColumnName("PredictedLabel")] public bool  PredictedLabel { get; set; }
    [ColumnName("Probability")]    public float Probability    { get; set; }
    [ColumnName("Score")]          public float Score          { get; set; }
}

// ─── Generic Multi-Disease Text Prediction (via Ollama) ──────────────────────
public class TextPredictionInput
{
    public string SymptomText  { get; set; } = string.Empty;
    public string PatientAge   { get; set; } = string.Empty;
    public string PatientSex   { get; set; } = string.Empty;
    public string MedicalHistory { get; set; } = string.Empty;
}

public class TextPredictionResult
{
    public string PrimaryDiagnosis    { get; set; } = string.Empty;
    public float  Confidence          { get; set; }
    public string ReasoningChain      { get; set; } = string.Empty;
    public List<DifferentialEntry>    Differentials { get; set; } = new();
    public List<string>               ConfirmatoryTests { get; set; } = new();
}

public class DifferentialEntry
{
    public int    Rank          { get; set; }
    public string Condition     { get; set; } = string.Empty;
    public float  Probability   { get; set; }
    public string Reasoning     { get; set; } = string.Empty;
    public List<string> Tests   { get; set; } = new();
}
