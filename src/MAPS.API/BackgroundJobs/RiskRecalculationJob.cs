using Hangfire;
using MAPS.API.Data;
using MAPS.API.Services.Risk;
using MAPS.Shared.Constants;
using Microsoft.EntityFrameworkCore;

namespace MAPS.API.BackgroundJobs;

public class RiskRecalculationJob
{
    private readonly AppDbContext           _context;
    private readonly IRiskAssessmentService _riskService;
    private readonly ILogger<RiskRecalculationJob> _logger;

    public RiskRecalculationJob(
        AppDbContext                  context,
        IRiskAssessmentService        riskService,
        ILogger<RiskRecalculationJob> logger)
    {
        _context     = context;
        _riskService = riskService;
        _logger      = logger;
    }

    /// <summary>
    /// Recalculates risk scores for ALL patients across ALL doctors.
    /// Scheduled every 15 minutes via Hangfire recurring job.
    /// </summary>
    [Queue(HangfireQueues.RiskCalc)]
    public async Task RecalculateAllAsync()
    {
        _logger.LogInformation(
            "Risk recalculation job started at {Time}", DateTime.UtcNow);

        // Get all active doctor IDs
        var doctorIds = await _context.Assignments
            .Where(a => a.IsActive)
            .Select(a => a.DoctorId)
            .Distinct()
            .ToListAsync();

        _logger.LogInformation(
            "Recalculating risk for {Count} doctors' patient lists", doctorIds.Count);

        // Process each doctor's patients concurrently (with throttle)
        var semaphore = new SemaphoreSlim(3); // Max 3 concurrent doctors
        var tasks = doctorIds.Select(async doctorId =>
        {
            await semaphore.WaitAsync();
            try
            {
                await _riskService.RecalculateAllForDoctorAsync(doctorId);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        _logger.LogInformation(
            "Risk recalculation job complete at {Time}", DateTime.UtcNow);
    }

    /// <summary>Register Hangfire recurring job — every 15 minutes</summary>
    public static void RegisterRecurringJob()
    {
        RecurringJob.AddOrUpdate<RiskRecalculationJob>(
            "recalculate-all-risks",
            job => job.RecalculateAllAsync(),
            "*/15 * * * *",             // Every 15 minutes
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
    }
}
