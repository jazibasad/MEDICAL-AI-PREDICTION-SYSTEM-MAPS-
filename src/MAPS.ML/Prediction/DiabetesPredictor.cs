using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.Extensions.Logging;

namespace MAPS.ML.Prediction;

public interface IDiabetesPredictor
{
    DiabetesPrediction Predict(DiabetesInput input);
    Task TrainAndSaveAsync(string dataPath, string modelPath);
}

public class DiabetesPredictor : IDiabetesPredictor
{
    private readonly MLContext                              _mlContext;
    private          PredictionEngine<DiabetesInput,
                                      DiabetesPrediction>? _engine;
    private readonly ILogger<DiabetesPredictor>            _logger;
    private readonly string                                _modelPath;

    public DiabetesPredictor(
        ILogger<DiabetesPredictor> logger,
        IConfiguration             config)
    {
        _mlContext = new MLContext(seed: 42);
        _logger    = logger;
        _modelPath = config["ML:DiabetesModelPath"]
                     ?? "models/mlnet/diabetes_model.zip";

        LoadModel();
    }

    // ── Load existing model from disk ─────────────────────────────────────────
    private void LoadModel()
    {
        if (!File.Exists(_modelPath))
        {
            _logger.LogWarning(
                "Diabetes model not found at {Path}. Train first or place model file.",
                _modelPath);
            return;
        }

        var loadedModel = _mlContext.Model.Load(_modelPath, out _);
        _engine = _mlContext.Model
            .CreatePredictionEngine<DiabetesInput, DiabetesPrediction>(loadedModel);

        _logger.LogInformation("Diabetes model loaded from {Path}", _modelPath);
    }

    // ── Predict ───────────────────────────────────────────────────────────────
    public DiabetesPrediction Predict(DiabetesInput input)
    {
        if (_engine is null)
        {
            _logger.LogWarning("Diabetes engine not ready — returning default prediction");
            return new DiabetesPrediction
            {
                PredictedLabel = false,
                Probability    = 0.5f,
                Score          = 0f
            };
        }

        return _engine.Predict(input);
    }

    // ── Train and Save ────────────────────────────────────────────────────────
    public async Task TrainAndSaveAsync(string dataPath, string modelPath)
    {
        _logger.LogInformation("Starting Diabetes model training from {DataPath}", dataPath);

        await Task.Run(() =>
        {
            // Load dataset
            var data = _mlContext.Data.LoadFromTextFile<DiabetesInput>(
                dataPath,
                hasHeader: true,
                separatorChar: ',');

            // Train/test split (80/20)
            var split = _mlContext.Data.TrainTestSplit(data, testFraction: 0.2);

            // Build pipeline
            var pipeline = _mlContext.Transforms
                .Concatenate("Features",
                    nameof(DiabetesInput.Pregnancies),
                    nameof(DiabetesInput.Glucose),
                    nameof(DiabetesInput.BloodPressure),
                    nameof(DiabetesInput.SkinThickness),
                    nameof(DiabetesInput.Insulin),
                    nameof(DiabetesInput.BMI),
                    nameof(DiabetesInput.DiabetesPedigree),
                    nameof(DiabetesInput.Age))
                .Append(_mlContext.Transforms.NormalizeMinMax("Features"))
                .Append(_mlContext.BinaryClassification.Trainers.FastTree(
                    labelColumnName:    "Label",
                    featureColumnName:  "Features",
                    numberOfLeaves:     50,
                    numberOfTrees:      100,
                    minimumExampleCountPerLeaf: 10,
                    learningRate:       0.1));

            // Train
            var model = pipeline.Fit(split.TrainSet);

            // Evaluate
            var predictions = model.Transform(split.TestSet);
            var metrics     = _mlContext.BinaryClassification
                .Evaluate(predictions, labelColumnName: "Label");

            _logger.LogInformation(
                "Diabetes model trained — Accuracy: {Acc:F3}, AUC: {Auc:F3}, F1: {F1:F3}",
                metrics.Accuracy, metrics.AreaUnderRocCurve, metrics.F1Score);

            // Save
            Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
            _mlContext.Model.Save(model, data.Schema, modelPath);
            _logger.LogInformation("Diabetes model saved to {Path}", modelPath);
        });

        // Reload engine with new model
        LoadModel();
    }
}
