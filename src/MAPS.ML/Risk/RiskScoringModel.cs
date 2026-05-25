using Microsoft.Extensions.Logging;
using MAPS.Shared.Enums;

namespace MAPS.ML.Risk;

public class RiskFeatures
{
    // AI Prediction outcomes
    public float RecentPositivePredictions { get; set; }
    public float HighConfidencePredictions  { get; set; }
    public float PredictionCount30Days      { get; set; }

    // Vital sign trends from clinical notes
    public float AvgBloodPressure          { get; set; }
    public float AvgGlucose               { get; set; }
    public float AvgBMI                   { get; set; }
    public float VitalTrendScore           { get; set; } // -1 (worsening) to +1 (improving)

    // Appointment patterns
    public float MissedAppointmentRatio    { get; set; } // 0-1
    public float DaysSinceLastVisit        { get; set; }
    public float EmergencyVisits30Days     { get; set; }

    // Prescription adherence
    public float ActivePrescriptions       { get; set; }
    public float ExpiredUntreatedRx        { get; set; }

    // Historical trajectory
    public float PreviousRiskScore         { get; set; }
    public float RiskScoreTrend            { get; set; } // delta from last score
    public float AgeWeight                 { get; set; } // age-based risk multiplier
}

public class RiskScoringResult
{
    public float          RiskScore      { get; set; }  // 0-100
    public UrgencyTier    UrgencyTier    { get; set; }
    public TrendDirection TrendDirection { get; set; }
    public float          PreviousScore  { get; set; }
    public List<string>   RiskFactors    { get; set; } = new();
}

public interface IRiskScoringModel
{
    RiskScoringResult Score(RiskFeatures features);
}

/// <summary>
/// Gradient boosting-inspired risk scoring model.
/// Produces a 0-100 risk score classified into 4 urgency tiers.
/// In production this is a trained ML.NET or LightGBM model;
/// here we use an explainable weighted feature scoring approach.
/// </summary>
public class RiskScoringModel : IRiskScoringModel
{
    private readonly ILogger<RiskScoringModel> _logger;

    // Feature weights (sum = 1.0)
    private static readonly Dictionary<string, float> Weights = new()
    {
        [nameof(RiskFeatures.RecentPositivePredictions)] = 0.18f,
        [nameof(RiskFeatures.HighConfidencePredictions)]  = 0.12f,
        [nameof(RiskFeatures.VitalTrendScore)]             = 0.15f,
        [nameof(RiskFeatures.MissedAppointmentRatio)]      = 0.10f,
        [nameof(RiskFeatures.EmergencyVisits30Days)]       = 0.12f,
        [nameof(RiskFeatures.ExpiredUntreatedRx)]          = 0.08f,
        [nameof(RiskFeatures.DaysSinceLastVisit)]          = 0.08f,
        [nameof(RiskFeatures.PreviousRiskScore)]           = 0.10f,
        [nameof(RiskFeatures.AgeWeight)]                   = 0.07f,
    };

    public RiskScoringModel(ILogger<RiskScoringModel> logger)
    {
        _logger = logger;
    }

    public RiskScoringResult Score(RiskFeatures f)
    {
        var factors = new List<string>();
        float rawScore = 0f;

        // ── Feature contributions ─────────────────────────────────────────────

        // Positive AI predictions (normalized 0-1 by max 5 predictions)
        float predContrib = Math.Min(f.RecentPositivePredictions / 5f, 1f);
        rawScore += predContrib * Weights[nameof(RiskFeatures.RecentPositivePredictions)];
        if (predContrib > 0.4f)
            factors.Add($"{f.RecentPositivePredictions:F0} recent positive AI predictions");

        // High-confidence predictions
        float confContrib = Math.Min(f.HighConfidencePredictions / 3f, 1f);
        rawScore += confContrib * Weights[nameof(RiskFeatures.HighConfidencePredictions)];
        if (confContrib > 0.5f)
            factors.Add("High-confidence disease predictions flagged");

        // Vital trends (inverted: worsening = higher risk)
        float vitalContrib = (1f - f.VitalTrendScore) / 2f; // convert -1..+1 → 1..0
        rawScore += vitalContrib * Weights[nameof(RiskFeatures.VitalTrendScore)];
        if (f.VitalTrendScore < -0.2f)
            factors.Add("Worsening vital sign trend");

        // Missed appointments
        rawScore += f.MissedAppointmentRatio * Weights[nameof(RiskFeatures.MissedAppointmentRatio)];
        if (f.MissedAppointmentRatio > 0.3f)
            factors.Add($"{f.MissedAppointmentRatio:P0} appointment non-attendance rate");

        // Emergency visits
        float emergContrib = Math.Min(f.EmergencyVisits30Days / 3f, 1f);
        rawScore += emergContrib * Weights[nameof(RiskFeatures.EmergencyVisits30Days)];
        if (f.EmergencyVisits30Days > 0)
            factors.Add($"{f.EmergencyVisits30Days:F0} emergency visit(s) in 30 days");

        // Untreated expired prescriptions
        float rxContrib = Math.Min(f.ExpiredUntreatedRx / 2f, 1f);
        rawScore += rxContrib * Weights[nameof(RiskFeatures.ExpiredUntreatedRx)];
        if (f.ExpiredUntreatedRx > 0)
            factors.Add("Active prescriptions expired without renewal");

        // Days since last visit (normalized 0-1 by 90 days max)
        float visitContrib = Math.Min(f.DaysSinceLastVisit / 90f, 1f);
        rawScore += visitContrib * Weights[nameof(RiskFeatures.DaysSinceLastVisit)];
        if (f.DaysSinceLastVisit > 60)
            factors.Add($"{f.DaysSinceLastVisit:F0} days since last clinical visit");

        // Carry-forward from previous score
        float prevContrib = f.PreviousRiskScore / 100f;
        rawScore += prevContrib * Weights[nameof(RiskFeatures.PreviousRiskScore)];

        // Age weight
        rawScore += f.AgeWeight * Weights[nameof(RiskFeatures.AgeWeight)];

        // Scale to 0-100
        float finalScore = Math.Clamp(rawScore * 100f, 0f, 100f);

        // Apply boosting for compound risk factors
        if (factors.Count >= 3)
            finalScore = Math.Min(finalScore * 1.15f, 100f);

        // Classify tier
        var tier = finalScore switch
        {
            > 80 => UrgencyTier.Emergency,
            > 60 => UrgencyTier.Urgent,
            > 30 => UrgencyTier.Normal,
            _    => UrgencyTier.Followup
        };

        // Trend direction
        var trend = (finalScore - f.PreviousRiskScore) switch
        {
            > 5  => TrendDirection.Worsening,
            < -5 => TrendDirection.Improving,
            _    => TrendDirection.Stable
        };

        if (factors.Count == 0)
            factors.Add("No significant risk factors identified");

        _logger.LogDebug(
            "Risk score: {Score:F1} | Tier: {Tier} | Trend: {Trend} | Factors: {Count}",
            finalScore, tier, trend, factors.Count);

        return new RiskScoringResult
        {
            RiskScore      = finalScore,
            UrgencyTier    = tier,
            TrendDirection = trend,
            PreviousScore  = f.PreviousRiskScore,
            RiskFactors    = factors
        };
    }

    /// <summary>Compute age-based weight multiplier</summary>
    public static float ComputeAgeWeight(DateTime? dateOfBirth)
    {
        if (!dateOfBirth.HasValue) return 0.3f;
        int age = DateTime.UtcNow.Year - dateOfBirth.Value.Year;
        return age switch
        {
            < 30 => 0.1f,
            < 45 => 0.2f,
            < 60 => 0.4f,
            < 70 => 0.6f,
            _    => 0.8f
        };
    }
}
