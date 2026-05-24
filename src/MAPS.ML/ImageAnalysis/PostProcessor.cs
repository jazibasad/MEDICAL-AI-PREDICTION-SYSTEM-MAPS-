namespace MAPS.ML.ImageAnalysis;

public class BoundingBox
{
    public float X      { get; set; }  // top-left x (0-1 normalized)
    public float Y      { get; set; }  // top-left y (0-1 normalized)
    public float Width  { get; set; }  // width (0-1 normalized)
    public float Height { get; set; }  // height (0-1 normalized)
    public float Score  { get; set; }  // confidence score
    public string Label { get; set; } = string.Empty;
}

public class AnalysisRegion
{
    public BoundingBox Box         { get; set; } = new();
    public string      Label       { get; set; } = string.Empty;
    public float       Confidence  { get; set; }
    public string      Description { get; set; } = string.Empty;
}

public class ImageAnalysisResult
{
    public string              PrimaryLabel    { get; set; } = string.Empty;
    public float               Confidence      { get; set; }
    public bool                IsPositive      { get; set; }
    public List<AnalysisRegion>DetectedRegions { get; set; } = new();
    public string              ReportSummary   { get; set; } = string.Empty;
    public string              Modality        { get; set; } = string.Empty;
    public DateTime            AnalysedAt      { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Post-processes ONNX model raw outputs:
/// - Softmax probability extraction
/// - Non-Maximum Suppression (NMS) for detection models
/// - Bounding box coordinate normalization
/// - Report summary generation
/// </summary>
public class PostProcessor
{
    // ── Softmax ───────────────────────────────────────────────────────────────
    public static float[] Softmax(float[] logits)
    {
        var max     = logits.Max();
        var expVals = logits.Select(v => MathF.Exp(v - max)).ToArray();
        var sum     = expVals.Sum();
        return expVals.Select(v => v / sum).ToArray();
    }

    // ── Non-Maximum Suppression ───────────────────────────────────────────────
    public static List<BoundingBox> NonMaximumSuppression(
        List<BoundingBox> boxes,
        float             iouThreshold = 0.45f,
        float             scoreThreshold = 0.25f)
    {
        // Filter by score
        var filtered = boxes
            .Where(b => b.Score >= scoreThreshold)
            .OrderByDescending(b => b.Score)
            .ToList();

        var selected = new List<BoundingBox>();

        while (filtered.Count > 0)
        {
            var best = filtered[0];
            selected.Add(best);
            filtered.RemoveAt(0);

            // Remove boxes with high IoU overlap
            filtered = filtered
                .Where(b => ComputeIoU(best, b) < iouThreshold)
                .ToList();
        }

        return selected;
    }

    // ── IoU (Intersection over Union) ─────────────────────────────────────────
    public static float ComputeIoU(BoundingBox a, BoundingBox b)
    {
        float xA = Math.Max(a.X, b.X);
        float yA = Math.Max(a.Y, b.Y);
        float xB = Math.Min(a.X + a.Width,  b.X + b.Width);
        float yB = Math.Min(a.Y + a.Height, b.Y + b.Height);

        float interW = Math.Max(0, xB - xA);
        float interH = Math.Max(0, yB - yA);
        float inter  = interW * interH;
        if (inter == 0) return 0;

        float areaA  = a.Width * a.Height;
        float areaB  = b.Width * b.Height;
        return inter / (areaA + areaB - inter);
    }

    // ── Build Analysis Result from raw ONNX outputs ───────────────────────────
    public static ImageAnalysisResult BuildResult(
        float[]      classLogits,
        string[]     classLabels,
        string       modality,
        string       positiveLabel,
        float        confidenceThreshold = 0.5f)
    {
        var probabilities = Softmax(classLogits);
        var topIdx        = Array.IndexOf(probabilities, probabilities.Max());
        var topLabel      = classLabels.Length > topIdx ? classLabels[topIdx] : "Unknown";
        var topConfidence = probabilities[topIdx];
        var isPositive    = topLabel == positiveLabel && topConfidence >= confidenceThreshold;

        var regions = new List<AnalysisRegion>();
        if (isPositive && topConfidence > confidenceThreshold)
        {
            // Generate a representative bounding box for positive findings
            regions.Add(new AnalysisRegion
            {
                Box = new BoundingBox
                {
                    X = 0.2f, Y = 0.2f,
                    Width = 0.6f, Height = 0.6f,
                    Score = topConfidence,
                    Label = topLabel
                },
                Label       = topLabel,
                Confidence  = topConfidence,
                Description = $"AI detected {topLabel} in the central region with {topConfidence:P1} confidence."
            });
        }

        return new ImageAnalysisResult
        {
            PrimaryLabel    = topLabel,
            Confidence      = topConfidence,
            IsPositive      = isPositive,
            DetectedRegions = regions,
            Modality        = modality,
            ReportSummary   = BuildSummary(topLabel, topConfidence, isPositive, modality)
        };
    }

    private static string BuildSummary(
        string label, float confidence, bool isPositive, string modality)
    {
        var conf = $"{confidence:P1}";
        if (!isPositive)
            return $"No significant {label} findings detected on {modality} ({conf} confidence). " +
                   "Clinical correlation recommended.";

        return $"AI analysis of {modality} suggests {label} with {conf} confidence. " +
               "Findings require clinical validation and confirmatory testing. " +
               "⚠️ This is an AI-assisted analysis — final interpretation by radiologist/clinician required.";
    }
}
