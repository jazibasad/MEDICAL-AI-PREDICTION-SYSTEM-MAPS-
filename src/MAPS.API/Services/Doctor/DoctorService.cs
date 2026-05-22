using Microsoft.EntityFrameworkCore;
using MAPS.API.Data;
using MAPS.API.Data.Entities;
using MAPS.API.Data.Repositories.Interfaces;
using MAPS.Shared.DTOs.Common;
using MAPS.Shared.DTOs.User;
using MAPS.Shared.Enums;

namespace MAPS.API.Services.Doctor;

// ─── DTOs specific to Doctor module ──────────────────────────────────────────
public class DoctorDashboardDto
{
    public List<PatientQueueItem>  PatientQueue      { get; set; } = new();
    public List<UpcomingAppt>      UpcomingToday     { get; set; } = new();
    public int                     TotalPatients     { get; set; }
    public int                     HighRiskCount     { get; set; }
    public int                     TodayPredictions  { get; set; }
    public int                     UnreadMessages    { get; set; }
}

public class PatientQueueItem
{
    public Guid   PatientId    { get; set; }
    public string FullName     { get; set; } = string.Empty;
    public double RiskScore    { get; set; }
    public string UrgencyTier  { get; set; } = string.Empty;
    public string UrgencyColor { get; set; } = string.Empty;
    public DateTime? NextAppt  { get; set; }
}

public class UpcomingAppt
{
    public Guid   AppointmentId { get; set; }
    public string PatientName   { get; set; } = string.Empty;
    public DateTime DateTime    { get; set; }
    public string PriorityTier  { get; set; } = string.Empty;
    public string Status        { get; set; } = string.Empty;
}

public class PatientTimelineDto
{
    public PatientProfileDto       Profile        { get; set; } = null!;
    public List<TimelineEvent>     Events         { get; set; } = new();
    public List<PrescriptionDto>   Prescriptions  { get; set; } = new();
    public RiskSummaryDto?         LatestRisk     { get; set; }
}

public class TimelineEvent
{
    public DateTime Timestamp   { get; set; }
    public string   EventType   { get; set; } = string.Empty;
    public string   Title       { get; set; } = string.Empty;
    public string   Description { get; set; } = string.Empty;
    public string   Icon        { get; set; } = string.Empty;
    public string   Color       { get; set; } = string.Empty;
}

public class PrescriptionDto
{
    public Guid     PrescriptionId { get; set; }
    public string   Medications    { get; set; } = string.Empty;
    public string   Status         { get; set; } = string.Empty;
    public DateTime CreatedAt      { get; set; }
    public DateTime? ExpiresAt     { get; set; }
}

public class CreatePrescriptionRequest
{
    public Guid   PatientId   { get; set; }
    public string Medications { get; set; } = "[]"; // JSON array
    public int    DurationDays{ get; set; } = 30;
    public string? Notes      { get; set; }
}

public class RiskSummaryDto
{
    public double   RiskScore     { get; set; }
    public string   UrgencyTier   { get; set; } = string.Empty;
    public string   TrendDirection{ get; set; } = string.Empty;
    public DateTime CalculatedAt  { get; set; }
}

public class DrugInteractionResult
{
    public bool             HasInteractions { get; set; }
    public List<Interaction> Interactions   { get; set; } = new();
}

public class Interaction
{
    public string Drug1    { get; set; } = string.Empty;
    public string Drug2    { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty; // Contraindicated, Major, Minor
    public string Message  { get; set; } = string.Empty;
}

// ─── Doctor Service Interface ─────────────────────────────────────────────────
public interface IDoctorService
{
    Task<ApiResponse<DoctorDashboardDto>>   GetDashboardAsync(Guid doctorId);
    Task<ApiResponse<List<PatientQueueItem>>> GetPatientQueueAsync(Guid doctorId);
    Task<ApiResponse<PatientTimelineDto>>   GetPatientTimelineAsync(Guid patientId, Guid doctorId);
    Task<ApiResponse<List<PrescriptionDto>>>GetPrescriptionsAsync(Guid patientId, Guid doctorId);
    Task<ApiResponse<PrescriptionDto>>      CreatePrescriptionAsync(CreatePrescriptionRequest req, Guid doctorId);
    Task<ApiResponse>                       UpdatePrescriptionStatusAsync(Guid prescriptionId, string status, Guid doctorId);
    Task<ApiResponse<DrugInteractionResult>>CheckDrugInteractionsAsync(Guid patientId, string newMedications);
}

// ─── Doctor Service Implementation ────────────────────────────────────────────
public class DoctorService : IDoctorService
{
    private readonly AppDbContext          _context;
    private readonly IAssignmentRepository _assignRepo;
    private readonly IRiskRepository       _riskRepo;
    private readonly IAuditRepository      _auditRepo;
    private readonly ILogger<DoctorService>_logger;

    public DoctorService(
        AppDbContext           context,
        IAssignmentRepository  assignRepo,
        IRiskRepository        riskRepo,
        IAuditRepository       auditRepo,
        ILogger<DoctorService> logger)
    {
        _context    = context;
        _assignRepo = assignRepo;
        _riskRepo   = riskRepo;
        _auditRepo  = auditRepo;
        _logger     = logger;
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────
    public async Task<ApiResponse<DoctorDashboardDto>> GetDashboardAsync(Guid doctorId)
    {
        var assignments = await _assignRepo.GetByDoctorIdAsync(doctorId);
        var patientIds  = assignments.Select(a => a.PatientId).ToList();

        var queue = new List<PatientQueueItem>();
        foreach (var assignment in assignments)
        {
            var latestRisk = await _riskRepo.GetLatestByPatientAsync(assignment.PatientId);
            var nextAppt   = await _context.Appointments
                .Where(a => a.PatientId == assignment.PatientId &&
                            a.DoctorId  == doctorId &&
                            a.DateTime  >= DateTime.UtcNow)
                .OrderBy(a => a.DateTime)
                .FirstOrDefaultAsync();

            queue.Add(new PatientQueueItem
            {
                PatientId    = assignment.PatientId,
                FullName     = assignment.Patient.User.FullName,
                RiskScore    = (double)(latestRisk?.RiskScore ?? 0),
                UrgencyTier  = latestRisk?.UrgencyTier.ToString() ?? "Unknown",
                UrgencyColor = GetUrgencyColor(latestRisk?.UrgencyTier),
                NextAppt     = nextAppt?.DateTime
            });
        }

        // Sort by risk score descending (highest risk first)
        queue = queue.OrderByDescending(q => q.RiskScore).ToList();

        var today = await _context.Appointments
            .Where(a => a.DoctorId == doctorId &&
                        a.DateTime.Date == DateTime.UtcNow.Date)
            .Include(a => a.Patient).ThenInclude(p => p.User)
            .OrderBy(a => a.DateTime)
            .Select(a => new UpcomingAppt
            {
                AppointmentId = a.AppointmentId,
                PatientName   = a.Patient.User.FullName,
                DateTime      = a.DateTime,
                PriorityTier  = a.PriorityTier.ToString(),
                Status        = a.Status.ToString()
            })
            .ToListAsync();

        var todayPredictions = await _context.AIPredictions
            .CountAsync(p => p.DoctorId == doctorId &&
                             p.CreatedAt.Date == DateTime.UtcNow.Date);

        var unreadMessages = await _context.ChatMessages
            .CountAsync(m => m.ReceiverId == (await _context.DoctorProfiles
                .Where(d => d.DoctorId == doctorId)
                .Select(d => d.UserId)
                .FirstOrDefaultAsync()) &&
                !m.IsRead);

        var dashboard = new DoctorDashboardDto
        {
            PatientQueue     = queue,
            UpcomingToday    = today,
            TotalPatients    = assignments.Count(),
            HighRiskCount    = queue.Count(q => q.UrgencyTier is "Emergency" or "Urgent"),
            TodayPredictions = todayPredictions,
            UnreadMessages   = unreadMessages
        };

        return ApiResponse<DoctorDashboardDto>.Ok(dashboard);
    }

    // ── Patient Queue ─────────────────────────────────────────────────────────
    public async Task<ApiResponse<List<PatientQueueItem>>> GetPatientQueueAsync(Guid doctorId)
    {
        var result = await GetDashboardAsync(doctorId);
        return ApiResponse<List<PatientQueueItem>>.Ok(result.Data!.PatientQueue);
    }

    // ── Patient Timeline ──────────────────────────────────────────────────────
    public async Task<ApiResponse<PatientTimelineDto>> GetPatientTimelineAsync(
        Guid patientId, Guid doctorId)
    {
        // Verify assignment
        if (!await _assignRepo.IsPatientAssignedToDoctor(patientId, doctorId))
            return ApiResponse<PatientTimelineDto>.Fail(
                "You are not assigned to this patient.");

        var patient = await _context.PatientProfiles
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.PatientId == patientId);

        if (patient is null)
            return ApiResponse<PatientTimelineDto>.Fail("Patient not found.");

        var events = new List<TimelineEvent>();

        // Predictions
        var predictions = await _context.AIPredictions
            .Where(p => p.PatientId == patientId)
            .OrderByDescending(p => p.CreatedAt)
            .Take(10)
            .ToListAsync();

        events.AddRange(predictions.Select(p => new TimelineEvent
        {
            Timestamp   = p.CreatedAt,
            EventType   = "Prediction",
            Title       = $"AI Prediction: {p.DiseaseType}",
            Description = $"{p.PrimaryDiagnosis} ({(p.Confidence * 100):F1}% confidence)",
            Icon        = "🤖",
            Color       = "#3b82f6"
        }));

        // Clinical Notes
        var records = await _context.HealthRecords
            .Where(r => r.PatientId == patientId)
            .Include(r => r.ClinicalNotes)
            .OrderByDescending(r => r.CreatedAt)
            .Take(5)
            .ToListAsync();

        foreach (var record in records)
        foreach (var note in record.ClinicalNotes)
            events.Add(new TimelineEvent
            {
                Timestamp   = note.CreatedAt,
                EventType   = "Note",
                Title       = "Clinical Note",
                Description = note.Summary.Length > 100
                    ? note.Summary[..100] + "..."
                    : note.Summary,
                Icon        = "📝",
                Color       = "#10b981"
            });

        // Appointments
        var appointments = await _context.Appointments
            .Where(a => a.PatientId == patientId && a.DoctorId == doctorId)
            .OrderByDescending(a => a.DateTime)
            .Take(5)
            .ToListAsync();

        events.AddRange(appointments.Select(a => new TimelineEvent
        {
            Timestamp   = a.DateTime,
            EventType   = "Appointment",
            Title       = $"Appointment ({a.Status})",
            Description = $"Priority: {a.PriorityTier}",
            Icon        = "📅",
            Color       = "#f59e0b"
        }));

        events = events.OrderByDescending(e => e.Timestamp).ToList();

        var prescriptions = await _context.Prescriptions
            .Where(p => p.PatientId == patientId && p.DoctorId == doctorId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PrescriptionDto
            {
                PrescriptionId = p.PrescriptionId,
                Medications    = p.Medications,
                Status         = p.Status,
                CreatedAt      = p.CreatedAt,
                ExpiresAt      = p.ExpiresAt
            })
            .ToListAsync();

        var latestRisk = await _riskRepo.GetLatestByPatientAsync(patientId);

        var dto = new PatientTimelineDto
        {
            Profile = new PatientProfileDto
            {
                PatientId  = patient.PatientId,
                UserId     = patient.UserId,
                FullName   = patient.User.FullName,
                Email      = patient.User.Email,
                Role       = UserRole.Patient,
                IsActive   = patient.User.IsActive,
                IsApproved = patient.User.IsApproved,
                CreatedAt  = patient.User.CreatedAt,
                BloodGroup = patient.BloodGroup,
                DateOfBirth= patient.DateOfBirth
            },
            Events        = events,
            Prescriptions = prescriptions,
            LatestRisk    = latestRisk is null ? null : new RiskSummaryDto
            {
                RiskScore      = (double)latestRisk.RiskScore,
                UrgencyTier    = latestRisk.UrgencyTier.ToString(),
                TrendDirection = latestRisk.TrendDirection.ToString(),
                CalculatedAt   = latestRisk.CalculatedAt
            }
        };

        return ApiResponse<PatientTimelineDto>.Ok(dto);
    }

    // ── Prescriptions ─────────────────────────────────────────────────────────
    public async Task<ApiResponse<List<PrescriptionDto>>> GetPrescriptionsAsync(
        Guid patientId, Guid doctorId)
    {
        if (!await _assignRepo.IsPatientAssignedToDoctor(patientId, doctorId))
            return ApiResponse<List<PrescriptionDto>>.Fail("Not assigned to this patient.");

        var prescriptions = await _context.Prescriptions
            .Where(p => p.PatientId == patientId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PrescriptionDto
            {
                PrescriptionId = p.PrescriptionId,
                Medications    = p.Medications,
                Status         = p.Status,
                CreatedAt      = p.CreatedAt,
                ExpiresAt      = p.ExpiresAt
            })
            .ToListAsync();

        return ApiResponse<List<PrescriptionDto>>.Ok(prescriptions);
    }

    // ── Create Prescription ───────────────────────────────────────────────────
    public async Task<ApiResponse<PrescriptionDto>> CreatePrescriptionAsync(
        CreatePrescriptionRequest req, Guid doctorId)
    {
        if (!await _assignRepo.IsPatientAssignedToDoctor(req.PatientId, doctorId))
            return ApiResponse<PrescriptionDto>.Fail("Not assigned to this patient.");

        var prescription = new Prescription
        {
            DoctorId    = doctorId,
            PatientId   = req.PatientId,
            Medications = req.Medications,
            Status      = "Active",
            CreatedAt   = DateTime.UtcNow,
            ExpiresAt   = DateTime.UtcNow.AddDays(req.DurationDays)
        };

        _context.Prescriptions.Add(prescription);
        await _context.SaveChangesAsync();

        await _auditRepo.LogAsync(new AuditLog
        {
            UserId     = doctorId,
            Action     = "PRESCRIPTION_CREATED",
            EntityType = "Prescription",
            EntityId   = prescription.PrescriptionId.ToString()
        });

        return ApiResponse<PrescriptionDto>.Ok(new PrescriptionDto
        {
            PrescriptionId = prescription.PrescriptionId,
            Medications    = prescription.Medications,
            Status         = prescription.Status,
            CreatedAt      = prescription.CreatedAt,
            ExpiresAt      = prescription.ExpiresAt
        });
    }

    // ── Update Prescription Status ────────────────────────────────────────────
    public async Task<ApiResponse> UpdatePrescriptionStatusAsync(
        Guid prescriptionId, string status, Guid doctorId)
    {
        var prescription = await _context.Prescriptions
            .FirstOrDefaultAsync(p => p.PrescriptionId == prescriptionId &&
                                       p.DoctorId == doctorId);
        if (prescription is null)
            return ApiResponse.Fail("Prescription not found or access denied.");

        prescription.Status = status;
        await _context.SaveChangesAsync();

        return ApiResponse.Ok("Prescription status updated.");
    }

    // ── Drug Interaction Checker ──────────────────────────────────────────────
    public async Task<ApiResponse<DrugInteractionResult>> CheckDrugInteractionsAsync(
        Guid patientId, string newMedications)
    {
        // Get current active prescriptions for this patient
        var currentMeds = await _context.Prescriptions
            .Where(p => p.PatientId == patientId && p.Status == "Active")
            .Select(p => p.Medications)
            .ToListAsync();

        // Rule-based interaction checking (real implementation uses drug DB in Chunk 12)
        var interactions = new List<Interaction>();

        // Known high-risk interaction pairs (simplified rule set)
        var knownInteractions = new List<(string, string, string, string)>
        {
            ("warfarin",   "aspirin",     "Major",          "Increased bleeding risk when combined."),
            ("metformin",  "alcohol",     "Major",          "Risk of lactic acidosis."),
            ("simvastatin","amiodarone",  "Contraindicated","Risk of severe myopathy."),
            ("ssri",       "maoi",        "Contraindicated","Risk of serotonin syndrome."),
            ("lisinopril", "potassium",   "Major",          "Risk of hyperkalemia."),
        };

        var newMedsLower = newMedications.ToLower();

        foreach (var currentMed in currentMeds)
        {
            var currentLower = currentMed.ToLower();
            foreach (var (drug1, drug2, severity, message) in knownInteractions)
            {
                if ((newMedsLower.Contains(drug1) && currentLower.Contains(drug2)) ||
                    (newMedsLower.Contains(drug2) && currentLower.Contains(drug1)))
                {
                    interactions.Add(new Interaction
                    {
                        Drug1    = drug1,
                        Drug2    = drug2,
                        Severity = severity,
                        Message  = message
                    });
                }
            }
        }

        var result = new DrugInteractionResult
        {
            HasInteractions = interactions.Any(),
            Interactions    = interactions
        };

        return ApiResponse<DrugInteractionResult>.Ok(result);
    }

    private static string GetUrgencyColor(UrgencyTier? tier) => tier switch
    {
        UrgencyTier.Emergency => "#ef4444",
        UrgencyTier.Urgent    => "#f97316",
        UrgencyTier.Normal    => "#3b82f6",
        UrgencyTier.Followup  => "#10b981",
        _                     => "#6b7280"
    };
}
