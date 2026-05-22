using Microsoft.EntityFrameworkCore;
using MAPS.API.Data;
using MAPS.API.Data.Entities;
using MAPS.API.Data.Repositories.Interfaces;
using MAPS.ML.Prediction;
using MAPS.Shared.DTOs.Common;
using MAPS.Shared.DTOs.Prediction;
using MAPS.Shared.Enums;

namespace MAPS.API.Services.Prediction;

public interface IPredictionService
{
    Task<ApiResponse<PredictionResultDto>>          PredictAsync(
        PredictionRequest req, Guid doctorId);
    Task<ApiResponse<List<DifferentialDiagnosisDto>>> GetDifferentialAsync(
        DifferentialDiagnosisRequest req, Guid doctorId);
    Task<ApiResponse<PredictionResultDto>>          GetByIdAsync(
        Guid predictionId, Guid requestorId);
    Task<ApiResponse<List<PredictionResultDto>>>    GetByPatientAsync(
        Guid patientId, Guid doctorId);
    Task<ApiResponse>                               ShareWithPatientAsync(
        SharePredictionRequest req, Guid doctorId);
}

public class PredictionService : IPredictionService
{
    private readonly AppDbContext                _context;
    private readonly IAssignmentRepository       _assignRepo;
    private readonly IAuditRepository            _auditRepo;
    private readonly IDiabetesPredictor          _diabetesPredictor;
    private readonly IHeartDiseasePredictor      _heartDiseasePredictor;
    private readonly IDifferentialDiagnosisEngine _ddxEngine;
    private readonly IOllamaService              _ollamaService;
    private readonly ILogger<PredictionService>  _logger;

    public PredictionService(
        AppDbContext                context,
        IAssignmentRepository       assignRepo,
        IAuditRepository            auditRepo,
        IDiabetesPredictor          diabetesPredictor,
        IHeartDiseasePredictor      heartDiseasePredictor,
        IDifferentialDiagnosisEngine ddxEngine,
        IOllamaService              ollamaService,
        ILogger<PredictionService>  logger)
    {
        _context               = context;
        _assignRepo            = assignRepo;
        _auditRepo             = auditRepo;
        _diabetesPredictor     = diabetesPredictor;
        _heartDiseasePredictor = heartDiseasePredictor;
        _ddxEngine             = ddxEngine;
        _ollamaService         = ollamaService;
        _logger                = logger;
    }

    // ── Main Prediction Entry Point ───────────────────────────────────────────
    public async Task<ApiResponse<PredictionResultDto>> PredictAsync(
        PredictionRequest req, Guid doctorId)
    {
        // Verify doctor assignment
        var doctorProfile = await _context.DoctorProfiles
            .FirstOrDefaultAsync(d => d.DoctorId == doctorId);
        if (doctorProfile is null)
            return ApiResponse<PredictionResultDto>.Fail("Doctor profile not found.");

        if (!await _assignRepo.IsPatientAssignedToDoctor(req.PatientId, doctorId))
            return ApiResponse<PredictionResultDto>.Fail(
                "Patient is not assigned to you.");

        // Route to appropriate prediction pipeline
        var (diagnosis, confidence) = req.InputModality switch
        {
            InputModality.Text  => await PredictViaOllamaAsync(req),
            InputModality.Image => await PredictViaOnnxAsync(req),
            _                   => await PredictViaStructuredDataAsync(req)
        };

        // Persist prediction
        var prediction = new AIPrediction
        {
            PatientId        = req.PatientId,
            DoctorId         = doctorId,
            DiseaseType      = req.DiseaseType,
            InputModality    = req.InputModality,
            PrimaryDiagnosis = diagnosis,
            Confidence       = (decimal)confidence,
            Status           = PredictionStatus.Complete,
            IsSharedWithPatient = false
        };

        _context.AIPredictions.Add(prediction);
        await _context.SaveChangesAsync();

        await _auditRepo.LogAsync(new AuditLog
        {
            UserId     = doctorId,
            Action     = "PREDICTION_CREATED",
            EntityType = "AIPrediction",
            EntityId   = prediction.PredictionId.ToString(),
            NewValues  = $"{{\"disease\":\"{req.DiseaseType}\",\"confidence\":{confidence:F4}}}"
        });

        _logger.LogInformation(
            "Prediction created: {Disease} for patient {PatientId}, confidence {Conf:F2}",
            req.DiseaseType, req.PatientId, confidence);

        return ApiResponse<PredictionResultDto>.Ok(MapToDto(prediction, doctorId));
    }

    // ── Structured Data Prediction (ML.NET) ───────────────────────────────────
    private async Task<(string diagnosis, float confidence)> PredictViaStructuredDataAsync(
        PredictionRequest req)
    {
        return await Task.Run(() =>
        {
            var features = req.StructuredFeatures ?? new Dictionary<string, double>();

            return req.DiseaseType switch
            {
                DiseaseType.Diabetes => PredictDiabetes(features),
                DiseaseType.HeartDisease => PredictHeartDisease(features),
                _ => ("Insufficient structured data for this disease type", 0.5f)
            };
        });
    }

    private (string, float) PredictDiabetes(Dictionary<string, double> features)
    {
        var input = new DiabetesInput
        {
            Glucose          = (float)features.GetValueOrDefault("glucose", 0),
            BMI              = (float)features.GetValueOrDefault("bmi", 0),
            Age              = (float)features.GetValueOrDefault("age", 0),
            BloodPressure    = (float)features.GetValueOrDefault("bloodPressure", 0),
            Insulin          = (float)features.GetValueOrDefault("insulin", 0),
            SkinThickness    = (float)features.GetValueOrDefault("skinThickness", 0),
            DiabetesPedigree = (float)features.GetValueOrDefault("diabetesPedigree", 0),
            Pregnancies      = (float)features.GetValueOrDefault("pregnancies", 0)
        };

        var result   = _diabetesPredictor.Predict(input);
        var diagnosis= result.PredictedLabel
            ? "Diabetes Mellitus Type 2 — Positive"
            : "Diabetes — Negative / Low Risk";

        return (diagnosis, result.Probability);
    }

    private (string, float) PredictHeartDisease(Dictionary<string, double> features)
    {
        var input = new HeartDiseaseInput
        {
            Age             = (float)features.GetValueOrDefault("age", 0),
            Sex             = (float)features.GetValueOrDefault("sex", 0),
            ChestPain       = (float)features.GetValueOrDefault("chestPain", 0),
            RestingBP       = (float)features.GetValueOrDefault("restingBP", 0),
            Cholesterol     = (float)features.GetValueOrDefault("cholesterol", 0),
            FastingBS       = (float)features.GetValueOrDefault("fastingBS", 0),
            RestingECG      = (float)features.GetValueOrDefault("restingECG", 0),
            MaxHR           = (float)features.GetValueOrDefault("maxHR", 0),
            ExerciseAngina  = (float)features.GetValueOrDefault("exerciseAngina", 0),
            Oldpeak         = (float)features.GetValueOrDefault("oldpeak", 0),
            STSlope         = (float)features.GetValueOrDefault("stSlope", 0)
        };

        var result    = _heartDiseasePredictor.Predict(input);
        var diagnosis = result.PredictedLabel
            ? "Coronary Heart Disease — Positive"
            : "Heart Disease — Negative / Low Risk";

        return (diagnosis, result.Probability);
    }

    // ── Text Prediction via Ollama ────────────────────────────────────────────
    private async Task<(string diagnosis, float confidence)> PredictViaOllamaAsync(
        PredictionRequest req)
    {
        var prompt = BuildClinicalPrompt(req);
        var result = await _ollamaService.GeneratePredictionAsync(
            req.DiseaseType, prompt);
        return (result.PrimaryDiagnosis, result.Confidence);
    }

    // ── Image Prediction via ONNX (stub — full impl in Chunk 10) ─────────────
    private async Task<(string diagnosis, float confidence)> PredictViaOnnxAsync(
        PredictionRequest req)
    {
        // Delegated to ONNX pipeline in Chunk 10
        await Task.Delay(100);
        return ("Image analysis pending — ONNX pipeline (see Chunk 10)", 0.0f);
    }

    // ── Differential Diagnosis ────────────────────────────────────────────────
    public async Task<ApiResponse<List<DifferentialDiagnosisDto>>> GetDifferentialAsync(
        DifferentialDiagnosisRequest req, Guid doctorId)
    {
        if (!await _assignRepo.IsPatientAssignedToDoctor(req.PatientId, doctorId))
            return ApiResponse<List<DifferentialDiagnosisDto>>.Fail(
                "Patient not assigned to you.");

        var patient = await _context.PatientProfiles
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.PatientId == req.PatientId);

        var age = patient?.DateOfBirth.HasValue == true
            ? (DateTime.UtcNow.Year - patient.DateOfBirth!.Value.Year).ToString()
            : null;

        var differentials = await _ddxEngine.GenerateDifferentialAsync(
            req.Symptoms,
            age,
            null,
            req.AdditionalContext);

        // Create an AIPrediction record for the top result
        if (differentials.Any())
        {
            var topResult = differentials.First();
            var prediction = new AIPrediction
            {
                PatientId        = req.PatientId,
                DoctorId         = doctorId,
                DiseaseType      = DiseaseType.Diabetes, // Placeholder — multi-disease
                InputModality    = InputModality.Text,
                PrimaryDiagnosis = topResult.Condition,
                Confidence       = (decimal)topResult.Probability,
                Status           = PredictionStatus.Complete
            };

            _context.AIPredictions.Add(prediction);

            // Add differential entries
            foreach (var ddx in differentials)
            {
                _context.DifferentialDiagnoses.Add(new DifferentialDiagnosis
                {
                    PredictionId   = prediction.PredictionId,
                    RankPosition   = ddx.Rank,
                    Condition      = ddx.Condition,
                    Probability    = (decimal)ddx.Probability,
                    ReasoningChain = ddx.Reasoning,
                    SuggestedTests = System.Text.Json.JsonSerializer.Serialize(ddx.Tests)
                });
            }

            await _context.SaveChangesAsync();
        }

        var dtos = differentials.Select(d => new DifferentialDiagnosisDto
        {
            DdxId          = Guid.NewGuid(),
            PredictionId   = Guid.Empty,
            RankPosition   = d.Rank,
            Condition      = d.Condition,
            Probability    = d.Probability,
            ReasoningChain = d.Reasoning,
            ConfirmatoryTests = d.Tests
        }).ToList();

        return ApiResponse<List<DifferentialDiagnosisDto>>.Ok(dtos);
    }

    // ── Get By Id ─────────────────────────────────────────────────────────────
    public async Task<ApiResponse<PredictionResultDto>> GetByIdAsync(
        Guid predictionId, Guid requestorId)
    {
        var prediction = await _context.AIPredictions
            .Include(p => p.DifferentialDiagnoses)
            .FirstOrDefaultAsync(p => p.PredictionId == predictionId);

        if (prediction is null)
            return ApiResponse<PredictionResultDto>.Fail("Prediction not found.");

        return ApiResponse<PredictionResultDto>.Ok(MapToDto(prediction, prediction.DoctorId));
    }

    // ── Get By Patient ────────────────────────────────────────────────────────
    public async Task<ApiResponse<List<PredictionResultDto>>> GetByPatientAsync(
        Guid patientId, Guid doctorId)
    {
        var predictions = await _context.AIPredictions
            .Where(p => p.PatientId == patientId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return ApiResponse<List<PredictionResultDto>>.Ok(
            predictions.Select(p => MapToDto(p, doctorId)).ToList());
    }

    // ── Share With Patient ────────────────────────────────────────────────────
    public async Task<ApiResponse> ShareWithPatientAsync(
        SharePredictionRequest req, Guid doctorId)
    {
        var prediction = await _context.AIPredictions
            .FirstOrDefaultAsync(p => p.PredictionId == req.PredictionId &&
                                       p.DoctorId     == doctorId);

        if (prediction is null)
            return ApiResponse.Fail("Prediction not found or access denied.");

        prediction.IsSharedWithPatient   = true;
        prediction.DoctorInterpretation  = req.DoctorInterpretation;
        await _context.SaveChangesAsync();

        return ApiResponse.Ok("Prediction shared with patient successfully.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static string BuildClinicalPrompt(PredictionRequest req)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Disease type being assessed: {req.DiseaseType}");

        if (!string.IsNullOrEmpty(req.TextPrompt))
            sb.AppendLine($"Clinical presentation: {req.TextPrompt}");

        return sb.ToString();
    }

    private static PredictionResultDto MapToDto(AIPrediction p, Guid doctorId) => new()
    {
        PredictionId         = p.PredictionId,
        PatientId            = p.PatientId,
        DoctorId             = p.DoctorId,
        DiseaseType          = p.DiseaseType,
        InputModality        = p.InputModality,
        PrimaryDiagnosis     = p.PrimaryDiagnosis,
        Confidence           = (double)p.Confidence,
        Status               = p.Status,
        IsSharedWithPatient  = p.IsSharedWithPatient,
        DoctorInterpretation = p.DoctorInterpretation,
        CreatedAt            = p.CreatedAt
    };
}
