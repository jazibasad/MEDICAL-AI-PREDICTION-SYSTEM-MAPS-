using Microsoft.ML;
using Microsoft.Extensions.Logging;

namespace MAPS.ML.Prediction;

public interface IHeartDiseasePredictor
{
    HeartDiseasePrediction Predict(HeartDiseaseInput input);
    Task TrainAndSaveAsync(string dataPath, string modelPath);
}

public class HeartDiseasePredictor : IHeartDiseasePredictor
{
    private readonly MLContext                                    _mlContext;
    private          PredictionEngine<HeartDiseaseInput,
                                      HeartDiseasePrediction>?   _engine;
    private readonly ILogger<HeartDiseasePredictor>              _logger;
    private readonly string                                      _modelPath;

    public HeartDiseasePredictor(
        ILogger<HeartDiseasePredictor> logger,
        IConfiguration                 config)
    {
        _mlContext = new MLContext(seed: 42);
        _logger    = logger;
        _modelPath = config["ML:HeartDiseaseModelPath"]
                     ?? "models/mlnet/heart_disease_model.zip";
        LoadModel();
    }

    private void LoadModel()
    {
        if (!File.Exists(_modelPath))
        {
            _logger.LogWarning("Heart disease model not found at {Path}", _modelPath);
            return;
        }

        var model = _mlContext.Model.Load(_modelPath, out _);
        _engine   = _mlContext.Model
            .CreatePredictionEngine<HeartDiseaseInput, HeartDiseasePrediction>(model);

        _logger.LogInformation("Heart disease model loaded from {Path}", _modelPath);
    }

    public HeartDiseasePrediction Predict(HeartDiseaseInput input)
    {
        if (_engine is null)
            return new HeartDiseasePrediction { Probability = 0.5f };

        return _engine.Predict(input);
    }

    public async Task TrainAndSaveAsync(string dataPath, string modelPath)
    {
        _logger.LogInformation("Starting Heart Disease model training from {DataPath}", dataPath);

        await Task.Run(() =>
        {
            var data = _mlContext.Data.LoadFromTextFile<HeartDiseaseInput>(
                dataPath, hasHeader: true, separatorChar: ',');

            var split = _mlContext.Data.TrainTestSplit(data, testFraction: 0.2);

            var pipeline = _mlContext.Transforms
                .Concatenate("Features",
                    nameof(HeartDiseaseInput.Age),
                    nameof(HeartDiseaseInput.Sex),
                    nameof(HeartDiseaseInput.ChestPain),
                    nameof(HeartDiseaseInput.RestingBP),
                    nameof(HeartDiseaseInput.Cholesterol),
                    nameof(HeartDiseaseInput.FastingBS),
                    nameof(HeartDiseaseInput.RestingECG),
                    nameof(HeartDiseaseInput.MaxHR),
                    nameof(HeartDiseaseInput.ExerciseAngina),
                    nameof(HeartDiseaseInput.Oldpeak),
                    nameof(HeartDiseaseInput.STSlope))
                .Append(_mlContext.Transforms.NormalizeMinMax("Features"))
                .Append(_mlContext.BinaryClassification.Trainers.LightGbm(
                    labelColumnName:   "Label",
                    featureColumnName: "Features",
                    numberOfLeaves:    31,
                    numberOfIterations:100,
                    learningRate:      0.05));

            var model       = pipeline.Fit(split.TrainSet);
            var predictions = model.Transform(split.TestSet);
            var metrics     = _mlContext.BinaryClassification
                .Evaluate(predictions, labelColumnName: "Label");

            _logger.LogInformation(
                "Heart disease model — Accuracy: {Acc:F3}, AUC: {Auc:F3}, F1: {F1:F3}",
                metrics.Accuracy, metrics.AreaUnderRocCurve, metrics.F1Score);

            Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
            _mlContext.Model.Save(model, data.Schema, modelPath);
            _logger.LogInformation("Heart disease model saved to {Path}", modelPath);
        });

        LoadModel();
    }
}
