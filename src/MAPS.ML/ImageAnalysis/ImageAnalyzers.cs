using Microsoft.Extensions.Logging;

namespace MAPS.ML.ImageAnalysis;

// ─── Pneumonia Analyzer — Chest X-Ray ────────────────────────────────────────
public interface IPneumoniaAnalyzer
{
    ImageAnalysisResult Analyse(byte[] imageBytes);
}

public class PneumoniaAnalyzer : OnnxInferencePipeline, IPneumoniaAnalyzer
{
    private static readonly string[] Labels = { "Normal", "Pneumonia" };

    public PneumoniaAnalyzer(
        IConfiguration    config,
        ImagePreprocessor preprocessor,
        ILogger<PneumoniaAnalyzer> logger)
        : base(config["ML:PneumoniaModelPath"]
               ?? "models/onnx/pneumonia_resnet.onnx",
               preprocessor, logger)
    { }

    protected override ImageAnalysisResult PostProcess(float[] modelOutput)
    {
        // ResNet output: [Normal_logit, Pneumonia_logit]
        return PostProcessor.BuildResult(
            modelOutput,
            Labels,
            modality:          "Chest X-Ray",
            positiveLabel:     "Pneumonia",
            confidenceThreshold: 0.5f);
    }

    protected override ImageAnalysisResult CreateStubResult() => new()
    {
        PrimaryLabel   = "Model Unavailable",
        Confidence     = 0f,
        IsPositive     = false,
        Modality       = "Chest X-Ray",
        ReportSummary  = "Pneumonia ONNX model not loaded. " +
                         "Place pneumonia_resnet.onnx in models/onnx/ and restart."
    };
}

// ─── Brain Tumour Analyzer — MRI ─────────────────────────────────────────────
public interface IBrainTumourAnalyzer
{
    ImageAnalysisResult Analyse(byte[] imageBytes);
}

public class BrainTumourAnalyzer : OnnxInferencePipeline, IBrainTumourAnalyzer
{
    // 4-class model: glioma, meningioma, pituitary, no_tumor
    private static readonly string[] Labels =
        { "glioma", "meningioma", "pituitary", "no_tumor" };

    public BrainTumourAnalyzer(
        IConfiguration    config,
        ImagePreprocessor preprocessor,
        ILogger<BrainTumourAnalyzer> logger)
        : base(config["ML:BrainTumourModelPath"]
               ?? "models/onnx/brain_tumour_densenet.onnx",
               preprocessor, logger)
    { }

    protected override ImageAnalysisResult PostProcess(float[] modelOutput)
    {
        var probs    = PostProcessor.Softmax(modelOutput);
        var topIdx   = Array.IndexOf(probs, probs.Max());
        var topLabel = Labels.Length > topIdx ? Labels[topIdx] : "unknown";
        var topConf  = probs[topIdx];
        var isPos    = topLabel != "no_tumor" && topConf > 0.5f;

        var result = new ImageAnalysisResult
        {
            PrimaryLabel = isPos ? $"Brain Tumour ({topLabel})" : "No Tumour Detected",
            Confidence   = topConf,
            IsPositive   = isPos,
            Modality     = "Brain MRI",
        };

        if (isPos)
        {
            result.DetectedRegions.Add(new AnalysisRegion
            {
                Label       = topLabel,
                Confidence  = topConf,
                Description = $"Possible {topLabel} tumour detected with {topConf:P1} confidence.",
                Box = new BoundingBox
                {
                    X = 0.25f, Y = 0.25f,
                    Width = 0.5f, Height = 0.5f,
                    Score = topConf, Label = topLabel
                }
            });
        }

        result.ReportSummary = isPos
            ? $"MRI analysis indicates possible {topLabel} with {topConf:P1} confidence. " +
              "Neurosurgery referral and contrast-enhanced MRI recommended. " +
              "⚠️ AI analysis only — radiologist review required."
            : $"No brain tumour detected on MRI ({topConf:P1} confidence). " +
              "Clinical correlation and follow-up as clinically indicated.";

        return result;
    }

    protected override ImageAnalysisResult CreateStubResult() => new()
    {
        PrimaryLabel  = "Model Unavailable",
        Confidence    = 0f,
        IsPositive    = false,
        Modality      = "Brain MRI",
        ReportSummary = "Brain Tumour ONNX model not loaded. " +
                        "Place brain_tumour_densenet.onnx in models/onnx/ and restart."
    };
}

// ─── Skin Cancer Analyzer — Lesion Photo ─────────────────────────────────────
public interface ISkinCancerAnalyzer
{
    ImageAnalysisResult Analyse(byte[] imageBytes);
}

public class SkinCancerAnalyzer : OnnxInferencePipeline, ISkinCancerAnalyzer
{
    // HAM10000 7-class model
    private static readonly string[] Labels =
    {
        "melanoma",
        "melanocytic_nevi",
        "basal_cell_carcinoma",
        "actinic_keratosis",
        "benign_keratosis",
        "dermatofibroma",
        "vascular_lesion"
    };

    private static readonly HashSet<string> MalignantLabels = new()
    {
        "melanoma", "basal_cell_carcinoma", "actinic_keratosis"
    };

    public SkinCancerAnalyzer(
        IConfiguration    config,
        ImagePreprocessor preprocessor,
        ILogger<SkinCancerAnalyzer> logger)
        : base(config["ML:SkinCancerModelPath"]
               ?? "models/onnx/skin_cancer_model.onnx",
               preprocessor, logger)
    { }

    protected override ImageAnalysisResult PostProcess(float[] modelOutput)
    {
        var probs    = PostProcessor.Softmax(modelOutput);
        var topIdx   = Array.IndexOf(probs, probs.Max());
        var topLabel = Labels.Length > topIdx ? Labels[topIdx] : "unknown";
        var topConf  = probs[topIdx];
        var isMalig  = MalignantLabels.Contains(topLabel) && topConf > 0.5f;

        var displayLabel = topLabel.Replace("_", " ").ToTitleCase();

        var result = new ImageAnalysisResult
        {
            PrimaryLabel = displayLabel,
            Confidence   = topConf,
            IsPositive   = isMalig,
            Modality     = "Dermatoscopy / Skin Lesion",
        };

        if (isMalig)
        {
            result.DetectedRegions.Add(new AnalysisRegion
            {
                Label       = displayLabel,
                Confidence  = topConf,
                Description = $"Possible {displayLabel} detected ({topConf:P1} confidence). Urgent biopsy recommended.",
                Box = new BoundingBox
                {
                    X = 0.15f, Y = 0.15f,
                    Width = 0.7f, Height = 0.7f,
                    Score = topConf, Label = topLabel
                }
            });
        }

        // Include top-3 differentials for skin cancer
        var top3 = probs.Select((p, i) => (prob: p, idx: i))
            .OrderByDescending(x => x.prob)
            .Take(3)
            .ToList();

        result.ReportSummary = isMalig
            ? $"Dermoscopic analysis suggests {displayLabel} ({topConf:P1}). " +
              "Immediate dermatology referral and skin biopsy recommended. " +
              $"Differential: {string.Join(", ", top3.Skip(1).Select(x => Labels[x.idx].Replace("_"," ")))}. " +
              "⚠️ AI analysis only — dermatologist evaluation required."
            : $"Skin lesion analysis: {displayLabel} ({topConf:P1}). " +
              "Appears benign. Routine follow-up recommended. " +
              "Monitor for ABCDE changes (Asymmetry, Border, Color, Diameter, Evolution).";

        return result;
    }

    protected override ImageAnalysisResult CreateStubResult() => new()
    {
        PrimaryLabel  = "Model Unavailable",
        Confidence    = 0f,
        IsPositive    = false,
        Modality      = "Skin Lesion",
        ReportSummary = "Skin Cancer ONNX model not loaded. " +
                        "Place skin_cancer_model.onnx in models/onnx/ and restart."
    };
}

// ─── Extension ────────────────────────────────────────────────────────────────
internal static class StringExtensions
{
    public static string ToTitleCase(this string s) =>
        string.Join(" ", s.Split(' ')
            .Select(w => w.Length > 0
                ? char.ToUpper(w[0]) + w[1..].ToLower()
                : w));
}
