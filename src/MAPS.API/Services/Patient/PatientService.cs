using Microsoft.EntityFrameworkCore;
using MAPS.API.Data;
using MAPS.API.Data.Entities;
using MAPS.API.Data.Repositories.Interfaces;
using MAPS.API.Services.Scheduling;
using MAPS.Shared.DTOs.Common;
using MAPS.Shared.Enums;

namespace MAPS.API.Services.Patient;

// ─── Patient-facing DTOs ──────────────────────────────────────────────────────
public class PatientDashboardDto
{
    public string              AssignedDoctorName  { get; set; } = string.Empty;
    public string              AssignedDoctorSpec  { get; set; } = string.Empty;
    public List<AppointmentDto>UpcomingAppointments{ get; set; } = new();
    public double              CurrentRiskScore    { get; set; }
    public string              UrgencyTier         { get; set; } = string.Empty;
    public int                 SharedPredictions   { get; set; }
    public int                 ActivePrescriptions { get; set; }
    public int                 UnreadMessages      { get; set; }
}

public class AppointmentDto
{
    public Guid     AppointmentId   { get; set; }
    public string   DoctorName      { get; set; } = string.Empty;
    public string   DoctorSpec      { get; set; } = string.Empty;
    public DateTime DateTime        { get; set; }
    public string   Status          { get; set; } = string.Empty;
    public string   PriorityTier    { get; set; } = string.Empty;
    public int      DurationMinutes { get; set; }
}

public class BookAppointmentRequest
{
    public DateTime PreferredDateTime { get; set; }
    public string?  Notes             { get; set; }
    public int      DurationMinutes   { get; set; } = 30;
}

public class SharedPredictionDto
{
    public Guid     PredictionId        { get; set; }
    public string   DiseaseType         { get; set; } = string.Empty;
    public string   PrimaryDiagnosis    { get; set; } = string.Empty;
    public string   ConfidenceDisplay   { get; set; } = string.Empty; // Patient-friendly
    public string   DoctorInterpretation{ get; set; } = string.Empty;
    public DateTime SharedAt            { get; set; }
}

public class SubmitFeedbackRequest
{
    public Guid   DoctorId { get; set; }
    public int    Rating   { get; set; } // 1-5
    public string Comment  { get; set; } = string.Empty;
}

public class HealthSummaryDto
{
    public List<SharedPredictionDto> SharedPredictions  { get; set; } = new();
    public List<AppointmentDto>      AppointmentHistory { get; set; } = new();
    public List<ActivePrescription>  ActivePrescriptions{ get; set; } = new();
}

public class ActivePrescription
{
    public Guid   PrescriptionId { get; set; }
    public string Medications    { get; set; } = string.Empty;
    public DateTime? ExpiresAt   { get; set; }
}

// ─── Patient Service Interface ────────────────────────────────────────────────
public interface IPatientService
{
    Task<ApiResponse<PatientDashboardDto>>   GetDashboardAsync(Guid userId);
    Task<ApiResponse<List<AppointmentDto>>>  GetAppointmentsAsync(Guid userId);
    Task<ApiResponse<AppointmentDto>>        BookAppointmentAsync(Guid userId, BookAppointmentRequest req);
    Task<ApiResponse>                        CancelAppointmentAsync(Guid appointmentId, Guid userId);
    Task<ApiResponse<HealthSummaryDto>>      GetHealthSummaryAsync(Guid userId);
    Task<ApiResponse>                        SubmitFeedbackAsync(Guid userId, SubmitFeedbackRequest req);
    Task<ApiResponse<List<AppointmentDto>>>  GetAvailableSlotsAsync(Guid userId);
}

// ─── Patient Service Implementation ──────────────────────────────────────────
public class PatientService : IPatientService
{
    private readonly AppDbContext                _context;
    private readonly IAssignmentRepository       _assignRepo;
    private readonly IRiskRepository             _riskRepo;
    private readonly IAppointmentPriorityEngine  _priorityEngine;
    private readonly IAuditRepository            _auditRepo;
    private readonly ILogger<PatientService>     _logger;

    public PatientService(
        AppDbContext                context,
        IAssignmentRepository       assignRepo,
        IRiskRepository             riskRepo,
        IAppointmentPriorityEngine  priorityEngine,
        IAuditRepository            auditRepo,
        ILogger<PatientService>     logger)
    {
        _context        = context;
        _assignRepo     = assignRepo;
        _riskRepo       = riskRepo;
        _priorityEngine = priorityEngine;
        _auditRepo      = auditRepo;
        _logger         = logger;
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────
    public async Task<ApiResponse<PatientDashboardDto>> GetDashboardAsync(Guid userId)
    {
        var patient = await _context.PatientProfiles
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (patient is null)
            return ApiResponse<PatientDashboardDto>.Fail("Patient profile not found.");

        // Get assigned doctor
        var assignment = await _context.Assignments
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .FirstOrDefaultAsync(a => a.PatientId == patient.PatientId && a.IsActive);

        // Upcoming appointments
        var upcoming = await _context.Appointments
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .Where(a => a.PatientId == patient.PatientId &&
                        a.DateTime  >= DateTime.UtcNow &&
                        a.Status    != AppointmentStatus.Cancelled)
            .OrderBy(a => a.DateTime)
            .Take(3)
            .Select(a => new AppointmentDto
            {
                AppointmentId   = a.AppointmentId,
                DoctorName      = a.Doctor.User.FullName,
                DoctorSpec      = a.Doctor.Specialization,
                DateTime        = a.DateTime,
                Status          = a.Status.ToString(),
                PriorityTier    = a.PriorityTier.ToString(),
                DurationMinutes = a.DurationMinutes
            })
            .ToListAsync();

        var latestRisk = await _riskRepo.GetLatestByPatientAsync(patient.PatientId);

        var sharedPredictions = await _context.AIPredictions
            .CountAsync(p => p.PatientId == patient.PatientId && p.IsSharedWithPatient);

        var activePrescriptions = await _context.Prescriptions
            .CountAsync(p => p.PatientId == patient.PatientId && p.Status == "Active");

        var doctorUserId = assignment?.Doctor?.UserId ?? Guid.Empty;
        var unreadMessages = doctorUserId != Guid.Empty
            ? await _context.ChatMessages
                .CountAsync(m => m.SenderId == doctorUserId &&
                                 m.ReceiverId == userId &&
                                 !m.IsRead)
            : 0;

        var dto = new PatientDashboardDto
        {
            AssignedDoctorName   = assignment?.Doctor?.User?.FullName ?? "Not assigned",
            AssignedDoctorSpec   = assignment?.Doctor?.Specialization ?? "—",
            UpcomingAppointments = upcoming,
            CurrentRiskScore     = (double)(latestRisk?.RiskScore ?? 0),
            UrgencyTier          = latestRisk?.UrgencyTier.ToString() ?? "Unknown",
            SharedPredictions    = sharedPredictions,
            ActivePrescriptions  = activePrescriptions,
            UnreadMessages       = unreadMessages
        };

        return ApiResponse<PatientDashboardDto>.Ok(dto);
    }

    // ── Get Appointments ──────────────────────────────────────────────────────
    public async Task<ApiResponse<List<AppointmentDto>>> GetAppointmentsAsync(Guid userId)
    {
        var patient = await _context.PatientProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId);
        if (patient is null)
            return ApiResponse<List<AppointmentDto>>.Fail("Patient not found.");

        var appointments = await _context.Appointments
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .Where(a => a.PatientId == patient.PatientId)
            .OrderByDescending(a => a.DateTime)
            .Select(a => new AppointmentDto
            {
                AppointmentId   = a.AppointmentId,
                DoctorName      = a.Doctor.User.FullName,
                DoctorSpec      = a.Doctor.Specialization,
                DateTime        = a.DateTime,
                Status          = a.Status.ToString(),
                PriorityTier    = a.PriorityTier.ToString(),
                DurationMinutes = a.DurationMinutes
            })
            .ToListAsync();

        return ApiResponse<List<AppointmentDto>>.Ok(appointments);
    }

    // ── Book Appointment ──────────────────────────────────────────────────────
    public async Task<ApiResponse<AppointmentDto>> BookAppointmentAsync(
        Guid userId, BookAppointmentRequest req)
    {
        var patient = await _context.PatientProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId);
        if (patient is null)
            return ApiResponse<AppointmentDto>.Fail("Patient not found.");

        // Find assigned doctor
        var assignment = await _context.Assignments
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .FirstOrDefaultAsync(a => a.PatientId == patient.PatientId && a.IsActive);

        if (assignment is null)
            return ApiResponse<AppointmentDto>.Fail(
                "You must be assigned to a doctor before booking an appointment.");

        // Get patient's priority tier from risk score
        var tier = await _priorityEngine.GetPatientPriorityTierAsync(patient.PatientId);

        // Check for time conflicts
        if (await _priorityEngine.HasConflictAsync(
                assignment.DoctorId, req.PreferredDateTime, req.DurationMinutes))
            return ApiResponse<AppointmentDto>.Fail(
                "The requested time slot is not available. Please choose another time.");

        var appointment = new Appointment
        {
            DoctorId        = assignment.DoctorId,
            PatientId       = patient.PatientId,
            DateTime        = req.PreferredDateTime,
            Status          = AppointmentStatus.Booked,
            PriorityTier    = tier,
            Notes           = req.Notes,
            DurationMinutes = req.DurationMinutes
        };

        _context.Appointments.Add(appointment);
        await _context.SaveChangesAsync();

        await _auditRepo.LogAsync(new AuditLog
        {
            UserId     = userId,
            Action     = "APPOINTMENT_BOOKED",
            EntityType = "Appointment",
            EntityId   = appointment.AppointmentId.ToString(),
            NewValues  = $"{{\"tier\":\"{tier}\",\"time\":\"{req.PreferredDateTime:O}\"}}"
        });

        _logger.LogInformation(
            "Patient {PatientId} booked appointment with doctor {DoctorId} at {Time} (tier: {Tier})",
            patient.PatientId, assignment.DoctorId, req.PreferredDateTime, tier);

        return ApiResponse<AppointmentDto>.Ok(new AppointmentDto
        {
            AppointmentId   = appointment.AppointmentId,
            DoctorName      = assignment.Doctor.User.FullName,
            DoctorSpec      = assignment.Doctor.Specialization,
            DateTime        = appointment.DateTime,
            Status          = appointment.Status.ToString(),
            PriorityTier    = tier.ToString(),
            DurationMinutes = appointment.DurationMinutes
        }, $"Appointment booked successfully. Priority: {tier}");
    }

    // ── Cancel Appointment ────────────────────────────────────────────────────
    public async Task<ApiResponse> CancelAppointmentAsync(Guid appointmentId, Guid userId)
    {
        var patient = await _context.PatientProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId);
        if (patient is null) return ApiResponse.Fail("Patient not found.");

        var appointment = await _context.Appointments
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId &&
                                       a.PatientId     == patient.PatientId);
        if (appointment is null)
            return ApiResponse.Fail("Appointment not found.");

        if (appointment.DateTime <= DateTime.UtcNow.AddHours(2))
            return ApiResponse.Fail(
                "Appointments can only be cancelled at least 2 hours before the scheduled time.");

        appointment.Status = AppointmentStatus.Cancelled;
        await _context.SaveChangesAsync();

        return ApiResponse.Ok("Appointment cancelled successfully.");
    }

    // ── Health Summary ────────────────────────────────────────────────────────
    public async Task<ApiResponse<HealthSummaryDto>> GetHealthSummaryAsync(Guid userId)
    {
        var patient = await _context.PatientProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId);
        if (patient is null)
            return ApiResponse<HealthSummaryDto>.Fail("Patient not found.");

        var sharedPredictions = await _context.AIPredictions
            .Where(p => p.PatientId == patient.PatientId && p.IsSharedWithPatient)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new SharedPredictionDto
            {
                PredictionId         = p.PredictionId,
                DiseaseType          = p.DiseaseType.ToString(),
                PrimaryDiagnosis     = p.PrimaryDiagnosis,
                ConfidenceDisplay    = $"{(p.Confidence * 100):F0}% confidence",
                DoctorInterpretation = p.DoctorInterpretation ?? "No interpretation provided.",
                SharedAt             = p.CreatedAt
            })
            .ToListAsync();

        var apptHistory = await _context.Appointments
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .Where(a => a.PatientId == patient.PatientId)
            .OrderByDescending(a => a.DateTime)
            .Take(10)
            .Select(a => new AppointmentDto
            {
                AppointmentId   = a.AppointmentId,
                DoctorName      = a.Doctor.User.FullName,
                DoctorSpec      = a.Doctor.Specialization,
                DateTime        = a.DateTime,
                Status          = a.Status.ToString(),
                PriorityTier    = a.PriorityTier.ToString(),
                DurationMinutes = a.DurationMinutes
            })
            .ToListAsync();

        var activePrescriptions = await _context.Prescriptions
            .Where(p => p.PatientId == patient.PatientId && p.Status == "Active")
            .Select(p => new ActivePrescription
            {
                PrescriptionId = p.PrescriptionId,
                Medications    = p.Medications,
                ExpiresAt      = p.ExpiresAt
            })
            .ToListAsync();

        return ApiResponse<HealthSummaryDto>.Ok(new HealthSummaryDto
        {
            SharedPredictions   = sharedPredictions,
            AppointmentHistory  = apptHistory,
            ActivePrescriptions = activePrescriptions
        });
    }

    // ── Submit Feedback ───────────────────────────────────────────────────────
    public async Task<ApiResponse> SubmitFeedbackAsync(Guid userId, SubmitFeedbackRequest req)
    {
        if (req.Rating is < 1 or > 5)
            return ApiResponse.Fail("Rating must be between 1 and 5.");

        var patient = await _context.PatientProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId);
        if (patient is null) return ApiResponse.Fail("Patient not found.");

        // Basic NLP sentiment classification
        var sentiment = ClassifySentiment(req.Comment, req.Rating);

        _context.Feedbacks.Add(new Feedback
        {
            PatientId      = patient.PatientId,
            DoctorId       = req.DoctorId,
            Rating         = req.Rating,
            Comment        = req.Comment,
            SentimentLabel = sentiment.Label,
            SentimentScore = sentiment.Score,
            SubmittedAt    = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        return ApiResponse.Ok("Feedback submitted. Thank you!");
    }

    // ── Available Slots ───────────────────────────────────────────────────────
    public async Task<ApiResponse<List<AppointmentDto>>> GetAvailableSlotsAsync(Guid userId)
    {
        var patient = await _context.PatientProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId);
        if (patient is null)
            return ApiResponse<List<AppointmentDto>>.Fail("Patient not found.");

        var assignment = await _context.Assignments
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .FirstOrDefaultAsync(a => a.PatientId == patient.PatientId && a.IsActive);
        if (assignment is null)
            return ApiResponse<List<AppointmentDto>>.Fail("No doctor assigned yet.");

        var tier     = await _priorityEngine.GetPatientPriorityTierAsync(patient.PatientId);
        var slots    = new List<AppointmentDto>();
        var baseTime = DateTime.UtcNow;

        // Generate 5 candidate slots
        for (int i = 0; i < 10 && slots.Count < 5; i++)
        {
            var slotTime = await _priorityEngine.GetNextAvailableSlotAsync(
                assignment.DoctorId, tier);
            if (slotTime is null) break;

            slots.Add(new AppointmentDto
            {
                DoctorName   = assignment.Doctor.User.FullName,
                DoctorSpec   = assignment.Doctor.Specialization,
                DateTime     = slotTime.Value,
                PriorityTier = tier.ToString(),
                Status       = "Available"
            });

            baseTime = slotTime.Value.AddMinutes(30);
        }

        return ApiResponse<List<AppointmentDto>>.Ok(slots);
    }

    // ── Simple Sentiment Classifier ───────────────────────────────────────────
    private static (string Label, double Score) ClassifySentiment(string text, int rating)
    {
        if (rating >= 4) return ("Positive", 0.85);
        if (rating <= 2) return ("Negative", 0.15);

        var lower = text.ToLower();
        var positiveWords = new[] { "good", "great", "excellent", "helpful", "satisfied", "happy" };
        var negativeWords = new[] { "bad", "poor", "terrible", "unhelpful", "dissatisfied", "slow" };

        var posScore = positiveWords.Count(w => lower.Contains(w));
        var negScore = negativeWords.Count(w => lower.Contains(w));

        if (posScore > negScore) return ("Positive", 0.7);
        if (negScore > posScore) return ("Negative", 0.3);
        return ("Neutral", 0.5);
    }
}
