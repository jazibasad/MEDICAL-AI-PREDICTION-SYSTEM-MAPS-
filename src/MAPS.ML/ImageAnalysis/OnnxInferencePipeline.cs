using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.Extensions.Logging;

namespace MAPS.ML.ImageAnalysis;

/// <summary>
/// Base ONNX Runtime inference pipeline.
/// Subclassed by PneumoniaAnalyzer, BrainTumourAnalyzer, SkinCancerAnalyzer.
/// </summary>
public abstract class OnnxInferencePipeline : IDisposable
{
    protected readonly InferenceSession?          _session;
    protected readonly ImagePreprocessor          _preprocessor;
    protected readonly ILogger                    _logger;
    protected readonly string                     _modelPath;

    private bool _disposed;

    protected OnnxInferencePipeline(
        string       modelPath,
        ImagePreprocessor preprocessor,
        ILogger      logger)
    {
        _modelPath    = modelPath;
        _preprocessor = preprocessor;
        _logger       = logger;

        if (!File.Exists(modelPath))
        {
            _logger.LogWarning(
                "ONNX model not found at {Path}. " +
                "Place the pre-trained model file and restart.",
                modelPath);
            return;
        }

        try
        {
            var sessionOptions = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                ExecutionMode          = ExecutionMode.ORT_SEQUENTIAL
            };

            _session = new InferenceSession(modelPath, sessionOptions);
            _logger.LogInformation(
                "ONNX model loaded: {Path} | Inputs: {In} | Outputs: {Out}",
                modelPath,
                string.Join(", ", _session.InputMetadata.Keys),
                string.Join(", ", _session.OutputMetadata.Keys));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load ONNX model from {Path}", modelPath);
        }
    }

    /// <summary>Run full inference pipeline on raw image bytes</summary>
    public ImageAnalysisResult Analyse(byte[] imageBytes)
    {
        if (_session is null)
        {
            _logger.LogWarning("ONNX session not available — returning stub result");
            return CreateStubResult();
        }

        // Step 1: Preprocess image → float tensor [1, 3, 224, 224]
        var inputTensor = _preprocessor.Preprocess(imageBytes);

        // Step 2: Build ONNX input
        var tensor = new DenseTensor<float>(
            inputTensor,
            new[] { 1, ImagePreprocessor.Channels,
                    ImagePreprocessor.TargetHeight,
                    ImagePreprocessor.TargetWidth });

        var inputName   = _session.InputMetadata.Keys.First();
        var inputs      = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(inputName, tensor)
        };

        // Step 3: Run inference
        using var outputs   = _session.Run(inputs);
        var outputTensor    = outputs.First().AsEnumerable<float>().ToArray();

        // Step 4: Post-process
        return PostProcess(outputTensor);
    }

    // Override in subclass to apply disease-specific post-processing
    protected abstract ImageAnalysisResult PostProcess(float[] modelOutput);
    protected abstract ImageAnalysisResult CreateStubResult();

    public void Dispose()
    {
        if (!_disposed)
        {
            _session?.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
