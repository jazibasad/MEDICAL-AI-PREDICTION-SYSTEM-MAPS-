using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MAPS.API.Data;
using MAPS.API.Data.Entities;
using MAPS.API.Data.Repositories.Interfaces;
using MAPS.API.Hubs;
using MAPS.ML.Risk;
using MAPS.Shared.DTOs.Common;
using MAPS.Shared.DTOs.Risk;
using MAPS.Shared.Enums;

namespace MAPS.API.Services.Risk;

public interface IRiskAssessmentService
{
    Task<ApiResponse<RiskAssessmentDto>> RecalculateAsync(Guid patientId, Guid doctorId);
    Task<ApiResponse<RiskAssessmentDto>> GetLatestAsync(Guid patientId, Guid doctorId);
    Task<ApiResponse<List<RiskAssessmentDto>>> GetAlertsAsync(Guid doctorId);
    Task RecalculateAllForDoctorAsync(Guid doctorId);
}

public class RiskAssessmentService : IRiskAssessmentService
{
    private readonly AppDbContext            _context;
    private readonly IAssignmentRepository   _assignRepo;
    private readonly IRiskRepository         _riskRepo;
    private readonly IAuditRepository        _auditRepo;
    private readonly IRiskScoringModel       _scoringModel;
    private readonly IHubContext<ChatHub>    _hub;
    private readonly ILogger<RiskAssessmentService> _logger;

    public RiskAssessmentService(
        AppDbContext                 context,
        IAssignmentRepository        assignRepo,
        IRiskRepository              riskRepo,
        IAuditRepository             auditRepo,
        IRiskScoringModel            scoringModel,
        IHubContext<ChatHub>         hub,
        ILogger<RiskAssessmentService> logger)
    {
        _context      = context;
        _assignRepo   = assignRepo;
        _riskRepo     = riskRepo;
        _auditRepo    = auditRepo;
        _scoringModel = scoringModel;
        _hub          = hub;
        _logger       = logger;
    }

    // ── Recalculate risk for a single patient ─────────────────────────────────
    public async Task<ApiResponse<RiskAssessmentDto>> RecalculateAsync(
        Guid patientId, Guid doctorId)
    {
        if (!await _assignRepo.IsPatientAssignedToDoctor(patientId, doctorId))
            return ApiResponse<RiskAssessmentDto>.Fail("Patient not assigned to you.");

        var result = await ComputeAndPersistAsync(patientId, doctorId);
        return ApiResponse<RiskAssessmentDto>.Ok(MapToDto(result));
    }

    // ── Get latest risk assessment ────────────────────────────────────────────
    public async Task<ApiResponse<RiskAssessmentDto>> GetLatestAsync(
        Guid patientId, Guid doctorId)
    {
        if (!await _assignRepo.IsPatientAssignedToDoctor(patientId, doctorId))
            return ApiResponse<RiskAssessmentDto>.Fail("Patient not assigned to you.");

        var latest = await _riskRepo.GetLatestByPatientAsync(patientId);
        if (latest is null)
        {
            // No assessment yet — compute one now
            var computed = await ComputeAndPersistAsync(patientId, doctorId);
            return ApiResponse<RiskAssessmentDto>.Ok(MapToDto(computed));
        }

        return ApiResponse<RiskAssessmentDto>.Ok(MapToDto(latest));
    }

    // ── Get high-risk alerts for a doctor ─────────────────────────────────────
    public async Task<ApiResponse<List<RiskAssessmentDto>>> GetAlertsAsync(Guid doctorId)
    {
        var highRisk = await _riskRepo.GetHighRiskPatientsAsync(doctorId);

        var dtos = highRisk.Select(r => new RiskAssessmentDto
        {
            AssessmentId   = r.AssessmentId,
            PatientId      = r.PatientId,
            PatientName    = r.Patient?.User?.FullName ?? "Unknown",
            RiskScore      = (double)r.RiskScore,
            UrgencyTier    = r.UrgencyTier,
            TrendDirection = r.TrendDirection,
            PreviousScore  = (double)r.PreviousScore,
            CalculatedAt   = r.CalculatedAt
        }).ToList();

        return ApiResponse<List<RiskAssessmentDto>>.Ok(dtos);
    }

    // ── Batch recalculate for all of a doctor's patients ─────────────────────
    public async Task RecalculateAllForDoctorAsync(Guid doctorId)
    {
        _logger.LogInformation(
            "Starting batch risk recalculation for doctor {DoctorId}", doctorId);

        var assignments = await _assignRepo.GetByDoctorIdAsync(doctorId);
        var tasks = assignments.Select(a =>
            ComputeAndPersistAsync(a.PatientId, doctorId));

        await Task.WhenAll(tasks);

        _logger.LogInformation(
            "Batch risk recalculation complete for {Count} patients",
            assignments.Count());
    }

    // ── Core computation ──────────────────────────────────────────────────────
    private async Task<RiskAssessment> ComputeAndPersistAsync(
        Guid patientId, Guid doctorId)
    {
        var patient = await _context.PatientProfiles
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.PatientId == patientId);

        var previous = await _riskRepo.GetLatestByPatientAsync(patientId);
        var prevScore = (float)(previous?.RiskScore ?? 0);

        // Aggregate risk features
        var features = await AggregateFeatures(patientId, prevScore, patient);

        // Score
        var result = _scoringModel.Score(features);

        // Persist
        var assessment = new RiskAssessment
        {
            PatientId      = patientId,
            DoctorId       = doctorId,
            RiskScore      = (decimal)result.RiskScore,
            UrgencyTier    = result.UrgencyTier,
            TrendDirection = result.TrendDirection,
            PreviousScore  = (decimal)prevScore,
            CalculatedAt   = DateTime.UtcNow
        };

        _context.RiskAssessments.Add(assessment);
        await _context.SaveChangesAsync();

        // Proactive SignalR alert if tier worsened or is high-risk
        await SendAlertIfNeeded(assessment, previous, doctorId, patient?.User?.FullName ?? "Patient");

        _logger.LogInformation(
            "Risk calculated for {PatientId}: {Score:F1} ({Tier}) | Trend: {Trend}",
            patientId, result.RiskScore, result.UrgencyTier, result.TrendDirection);

        return assessment;
    }

    // ── Feature aggregation ───────────────────────────────────────────────────
    private async Task<RiskFeatures> AggregateFeatures(
        Guid patientId, float prevScore, PatientProfile? patient)
    {
        var now    = DateTime.UtcNow;
        var days30 = now.AddDays(-30);

        // AI prediction outcomes in last 30 days
        var predictions = await _context.AIPredictions
            .Where(p => p.PatientId == patientId && p.CreatedAt >= days30)
            .ToListAsync();

        var positivePreds = predictions.Count(p => p.PrimaryDiagnosis.Contains("Positive"));
        var highConfPreds = predictions.Count(p => p.Confidence > 0.75m);

        // Appointment patterns
        var allAppts = await _context.Appointments
            .Where(a => a.PatientId == patientId)
            .ToListAsync();

        var missed = allAppts.Count(a => a.Status == AppointmentStatus.NoShow ||
                                         a.Status == AppointmentStatus.Cancelled);
        var missedRatio = allAppts.Any()
            ? (float)missed / allAppts.Count
            : 0f;

        var lastVisit = allAppts
            .Where(a => a.Status == AppointmentStatus.Completed)
            .OrderByDescending(a => a.DateTime)
            .FirstOrDefault();
        var daysSinceVisit = lastVisit is null
            ? 90f
            : (float)(now - lastVisit.DateTime).TotalDays;

        var emergencyVisits = allAppts
            .Count(a => a.DateTime >= days30 &&
                        a.PriorityTier == UrgencyTier.Emergency);

        // Prescription adherence
        var prescriptions = await _context.Prescriptions
            .Where(p => p.PatientId == patientId)
            .ToListAsync();

        var activePrescriptions = prescriptions.Count(p => p.Status == "Active");
        var expiredUntreated = prescriptions.Count(p =>
            p.Status == "Active" &&
            p.ExpiresAt.HasValue &&
            p.ExpiresAt.Value < now);

        return new RiskFeatures
        {
            RecentPositivePredictions = positivePreds,
            HighConfidencePredictions  = highConfPreds,
            PredictionCount30Days      = predictions.Count,
            VitalTrendScore            = 0f, // Updated by NLP service when notes processed
            MissedAppointmentRatio     = missedRatio,
            DaysSinceLastVisit         = daysSinceVisit,
            EmergencyVisits30Days      = emergencyVisits,
            ActivePrescriptions        = activePrescriptions,
            ExpiredUntreatedRx         = expiredUntreated,
            PreviousRiskScore          = prevScore,
            RiskScoreTrend             = 0f,
            AgeWeight = RiskScoringModel.ComputeAgeWeight(patient?.DateOfBirth)
        };
    }

    // ── SignalR proactive alert ───────────────────────────────────────────────
    private async Task SendAlertIfNeeded(
        RiskAssessment current,
        RiskAssessment? previous,
        Guid doctorId,
        string patientName)
    {
        bool shouldAlert =
            current.UrgencyTier <= UrgencyTier.Urgent || // Emergency or Urgent
            (previous != null && current.UrgencyTier < previous.UrgencyTier); // Tier worsened

        if (!shouldAlert) return;

        var doctorUser = await _context.DoctorProfiles
            .Where(d => d.DoctorId == doctorId)
            .Select(d => d.UserId)
            .FirstOrDefaultAsync();

        if (doctorUser == Guid.Empty) return;

        var alertMessage = current.UrgencyTier == UrgencyTier.Emergency
            ? $"🚨 EMERGENCY: {patientName} risk score {current.RiskScore:F0}/100. Immediate intervention required."
            : $"⚠️ URGENT: {patientName} risk score {current.RiskScore:F0}/100. Priority consultation needed.";

        await ChatHub.SendNotificationAsync(_hub, doctorUser, "risk_alert", new
        {
            patientId      = current.PatientId,
            patientName,
            riskScore      = current.RiskScore,
            urgencyTier    = current.UrgencyTier.ToString(),
            trendDirection = current.TrendDirection.ToString(),
            message        = alertMessage,
            timestamp      = current.CalculatedAt
        });

        _logger.LogWarning(
            "Risk alert sent to doctor {DoctorId}: {PatientName} → {Tier}",
            doctorId, patientName, current.UrgencyTier);
    }

    private static RiskAssessmentDto MapToDto(RiskAssessment r) => new()
    {
        AssessmentId   = r.AssessmentId,
        PatientId      = r.PatientId,
        PatientName    = r.Patient?.User?.FullName ?? string.Empty,
        RiskScore      = (double)r.RiskScore,
        UrgencyTier    = r.UrgencyTier,
        TrendDirection = r.TrendDirection,
        PreviousScore  = (double)r.PreviousScore,
        CalculatedAt   = r.CalculatedAt
    };
}
