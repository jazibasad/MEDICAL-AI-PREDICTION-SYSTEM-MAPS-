using Hangfire;
using MAPS.ML.Prediction;
using MAPS.Shared.Constants;

namespace MAPS.API.BackgroundJobs;

public class ModelRetrainingJob
{
    private readonly IDiabetesPredictor     _diabetesPredictor;
    private readonly IHeartDiseasePredictor _heartDiseasePredictor;
    private readonly ILogger<ModelRetrainingJob> _logger;
    private readonly IConfiguration         _config;

    public ModelRetrainingJob(
        IDiabetesPredictor      diabetesPredictor,
        IHeartDiseasePredictor  heartDiseasePredictor,
        ILogger<ModelRetrainingJob> logger,
        IConfiguration          config)
    {
        _diabetesPredictor     = diabetesPredictor;
        _heartDiseasePredictor = heartDiseasePredictor;
        _logger                = logger;
        _config                = config;
    }

    /// <summary>
    /// Retrains ML.NET models on updated datasets.
    /// Scheduled weekly via Hangfire — or triggered manually by Admin.
    /// Queue: HangfireQueues.ModelRetrain
    /// </summary>
    [Queue(HangfireQueues.ModelRetrain)]
    public async Task RetrainAllModelsAsync()
    {
        _logger.LogInformation("Model retraining job started at {Time}", DateTime.UtcNow);

        var diabetesDataPath     = _config["ML:DiabetesDataPath"]
                                   ?? "data/datasets/diabetes.csv";
        var diabetesModelPath    = _config["ML:DiabetesModelPath"]
                                   ?? "models/mlnet/diabetes_model.zip";
        var heartDataPath        = _config["ML:HeartDiseaseDataPath"]
                                   ?? "data/datasets/heart_disease.csv";
        var heartModelPath       = _config["ML:HeartDiseaseModelPath"]
                                   ?? "models/mlnet/heart_disease_model.zip";

        var tasks = new List<Task>();

        if (File.Exists(diabetesDataPath))
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await _diabetesPredictor.TrainAndSaveAsync(
                        diabetesDataPath, diabetesModelPath);
                    _logger.LogInformation("Diabetes model retrained successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Diabetes retraining failed");
                }
            }));
        }

        if (File.Exists(heartDataPath))
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await _heartDiseasePredictor.TrainAndSaveAsync(
                        heartDataPath, heartModelPath);
                    _logger.LogInformation("Heart disease model retrained successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Heart disease retraining failed");
                }
            }));
        }

        await Task.WhenAll(tasks);
        _logger.LogInformation("Model retraining job completed at {Time}", DateTime.UtcNow);
    }

    /// <summary>Register Hangfire recurring job — called once on startup</summary>
    public static void RegisterRecurringJob()
    {
        // Retrain every Sunday at 02:00 UTC
        RecurringJob.AddOrUpdate<ModelRetrainingJob>(
            "retrain-all-models",
            job => job.RetrainAllModelsAsync(),
            "0 2 * * 0",
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
    }
}
