using Microsoft.Extensions.Logging;
using MAPS.Shared.Enums;

namespace MAPS.ML.Prediction;

public interface IDifferentialDiagnosisEngine
{
    Task<List<DifferentialEntry>> GenerateDifferentialAsync(
        List<string>  symptoms,
        string?       patientAge,
        string?       patientSex,
        string?       additionalContext);
}

public class DifferentialDiagnosisEngine : IDifferentialDiagnosisEngine
{
    private readonly ILogger<DifferentialDiagnosisEngine> _logger;

    // Clinical knowledge base — symptom → disease probability weights
    // In production this is ML.NET multi-label classification
    private static readonly Dictionary<string, Dictionary<string, float>> _symptomWeights = new()
    {
        ["fever"] = new()
        {
            ["Pneumonia"] = 0.75f, ["Influenza"] = 0.70f,
            ["UrinaryTractInfection"] = 0.50f, ["Appendicitis"] = 0.45f
        },
        ["chest_pain"] = new()
        {
            ["HeartDisease"] = 0.80f, ["Pneumonia"] = 0.55f,
            ["Costochondritis"] = 0.40f, ["GERD"] = 0.35f
        },
        ["shortness_of_breath"] = new()
        {
            ["Pneumonia"] = 0.78f, ["HeartDisease"] = 0.72f,
            ["Asthma"] = 0.65f, ["Anxiety"] = 0.30f
        },
        ["increased_thirst"] = new()
        {
            ["Diabetes"] = 0.85f, ["DiabetesInsipidus"] = 0.55f,
            ["Hyperglycemia"] = 0.70f
        },
        ["frequent_urination"] = new()
        {
            ["Diabetes"] = 0.80f, ["UrinaryTractInfection"] = 0.70f,
            ["OveractiveBladder"] = 0.45f
        },
        ["headache"] = new()
        {
            ["Migraine"] = 0.70f, ["TensionHeadache"] = 0.65f,
            ["Hypertension"] = 0.50f, ["Meningitis"] = 0.30f
        },
        ["fatigue"] = new()
        {
            ["Diabetes"] = 0.60f, ["Anemia"] = 0.65f,
            ["Hypothyroidism"] = 0.55f, ["HeartDisease"] = 0.45f
        },
        ["nausea"] = new()
        {
            ["Appendicitis"] = 0.60f, ["GERD"] = 0.55f,
            ["Migraine"] = 0.50f, ["Pregnancy"] = 0.45f
        },
        ["cough"] = new()
        {
            ["Pneumonia"] = 0.80f, ["Asthma"] = 0.65f,
            ["COPD"] = 0.60f, ["COVID19"] = 0.70f
        },
        ["skin_lesion"] = new()
        {
            ["SkinCancer"] = 0.75f, ["Eczema"] = 0.60f,
            ["Psoriasis"] = 0.55f, ["ContactDermatitis"] = 0.50f
        },
        ["seizures"] = new()
        {
            ["BrainTumour"] = 0.65f, ["Epilepsy"] = 0.80f,
            ["Meningitis"] = 0.45f, ["Hypoglycemia"] = 0.40f
        },
        ["vision_changes"] = new()
        {
            ["BrainTumour"] = 0.60f, ["Glaucoma"] = 0.65f,
            ["Diabetes"] = 0.55f, ["MultipleSclerosis"] = 0.40f
        }
    };

    // Confirmatory tests per condition
    private static readonly Dictionary<string, List<string>> _confirmatoryTests = new()
    {
        ["Diabetes"]             = new() { "Fasting Blood Glucose", "HbA1c", "Oral Glucose Tolerance Test" },
        ["HeartDisease"]         = new() { "ECG", "Troponin I/T", "Echocardiogram", "Coronary Angiography" },
        ["Pneumonia"]            = new() { "Chest X-Ray", "CBC", "Sputum Culture", "CT Chest" },
        ["BrainTumour"]          = new() { "MRI Brain with Contrast", "CT Head", "Neurological Exam", "Biopsy" },
        ["SkinCancer"]           = new() { "Dermoscopy", "Skin Biopsy", "Sentinel Node Biopsy" },
        ["Hypertension"]         = new() { "Blood Pressure Monitoring", "Renal Function Tests", "ECG" },
        ["Migraine"]             = new() { "Neurological Exam", "MRI Brain", "CT Head" },
        ["Appendicitis"]         = new() { "Ultrasound Abdomen", "CT Abdomen", "CBC with differential" },
        ["Asthma"]               = new() { "Spirometry", "Peak Flow Monitoring", "Allergy Testing" },
        ["Hypothyroidism"]       = new() { "TSH", "Free T4", "Free T3", "Thyroid Ultrasound" },
        ["UrinaryTractInfection"]= new() { "Urinalysis", "Urine Culture", "CBC" },
        ["Anemia"]               = new() { "CBC", "Iron Studies", "B12/Folate", "Peripheral Blood Smear" },
    };

    public DifferentialDiagnosisEngine(ILogger<DifferentialDiagnosisEngine> logger)
    {
        _logger = logger;
    }

    public async Task<List<DifferentialEntry>> GenerateDifferentialAsync(
        List<string> symptoms,
        string?      patientAge,
        string?      patientSex,
        string?      additionalContext)
    {
        return await Task.Run(() =>
        {
            // Aggregate probability scores across symptoms
            var scores = new Dictionary<string, float>();

            var normalizedSymptoms = symptoms
                .Select(s => s.ToLower().Replace(" ", "_"))
                .ToList();

            foreach (var symptom in normalizedSymptoms)
            {
                if (!_symptomWeights.TryGetValue(symptom, out var diseaseWeights))
                    continue;

                foreach (var (disease, weight) in diseaseWeights)
                {
                    scores[disease] = scores.GetValueOrDefault(disease, 0f) + weight;
                }
            }

            if (!scores.Any())
            {
                _logger.LogWarning("No symptom matches found for differential diagnosis");
                return new List<DifferentialEntry>();
            }

            // Normalize scores to probabilities (0–1)
            var maxScore = scores.Values.Max();
            var normalized = scores
                .ToDictionary(kv => kv.Key, kv => kv.Value / maxScore);

            // Apply demographic filters
            normalized = ApplyDemographicFilters(normalized, patientAge, patientSex);

            // Clinical filtering: remove implausible candidates (<15% probability)
            var filtered = normalized
                .Where(kv => kv.Value >= 0.15f)
                .OrderByDescending(kv => kv.Value)
                .Take(5) // Top 5 differentials
                .ToList();

            // Build result with reasoning chains
            var result = filtered.Select((kv, idx) => new DifferentialEntry
            {
                Rank        = idx + 1,
                Condition   = kv.Key,
                Probability = (float)Math.Round(kv.Value, 4),
                Reasoning   = BuildReasoningChain(kv.Key, normalizedSymptoms),
                Tests       = _confirmatoryTests.GetValueOrDefault(kv.Key, new List<string>
                {
                    "Physical Examination",
                    "Complete Blood Count",
                    "Metabolic Panel"
                })
            }).ToList();

            _logger.LogInformation(
                "Generated {Count} differential diagnoses for symptoms: {Symptoms}",
                result.Count, string.Join(", ", symptoms));

            return result;
        });
    }

    private static Dictionary<string, float> ApplyDemographicFilters(
        Dictionary<string, float> scores,
        string? ageStr,
        string? sex)
    {
        if (!int.TryParse(ageStr, out var age)) return scores;

        // Age-based adjustments
        if (age < 30)
        {
            // Less likely in young patients
            if (scores.ContainsKey("HeartDisease"))
                scores["HeartDisease"] *= 0.5f;
            if (scores.ContainsKey("BrainTumour"))
                scores["BrainTumour"]  *= 0.6f;
        }

        if (age > 60)
        {
            // More likely in elderly
            if (scores.ContainsKey("HeartDisease"))
                scores["HeartDisease"] *= 1.3f;
            if (scores.ContainsKey("Diabetes"))
                scores["Diabetes"]     *= 1.2f;
        }

        // Sex-based adjustments
        if (sex?.ToLower() == "female")
        {
            if (scores.ContainsKey("Pregnancy"))
                scores["Pregnancy"] *= 1.5f;
        }
        else if (sex?.ToLower() == "male")
        {
            if (scores.ContainsKey("HeartDisease"))
                scores["HeartDisease"] *= 1.2f;
        }

        return scores;
    }

    private static string BuildReasoningChain(string condition, List<string> symptoms)
    {
        var matchingSymptoms = symptoms
            .Where(s => _symptomWeights.TryGetValue(s, out var w) && w.ContainsKey(condition))
            .Select(s => s.Replace("_", " "))
            .ToList();

        if (!matchingSymptoms.Any())
            return $"General clinical presentation consistent with {condition}.";

        return $"Symptoms {string.Join(", ", matchingSymptoms)} are " +
               $"clinically associated with {condition}. " +
               $"Confirmatory testing recommended to rule in/out.";
    }
}
