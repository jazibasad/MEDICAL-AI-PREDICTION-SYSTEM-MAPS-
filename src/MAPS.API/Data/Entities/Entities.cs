using MAPS.Shared.Enums;

namespace MAPS.API.Data.Entities;

// ─── CORE USER ENTITIES ───────────────────────────────────────────────────────

public class AppUser
{
    public Guid UserId { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsApproved { get; set; } = false;
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public DoctorProfile? DoctorProfile { get; set; }
    public PatientProfile? PatientProfile { get; set; }
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public ICollection<ChatMessage> SentMessages { get; set; } = new List<ChatMessage>();
    public ICollection<ChatMessage> ReceivedMessages { get; set; } = new List<ChatMessage>();
    public ICollection<Announcement> CreatedAnnouncements { get; set; } = new List<Announcement>();
}

public class DoctorProfile
{
    public Guid DoctorId { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Specialization { get; set; } = string.Empty;
    public string LicenseNumber { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;

    // Navigation
    public AppUser User { get; set; } = null!;
    public ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
    public ICollection<Prescription> Prescriptions { get; set; } = new List<Prescription>();
    public ICollection<AIPrediction> Predictions { get; set; } = new List<AIPrediction>();
    public ICollection<ClinicalNote> ClinicalNotes { get; set; } = new List<ClinicalNote>();
    public ICollection<MedicalImage> MedicalImages { get; set; } = new List<MedicalImage>();
    public ICollection<ChatSession> ChatSessions { get; set; } = new List<ChatSession>();
    public ICollection<RiskAssessment> RiskAssessments { get; set; } = new List<RiskAssessment>();
}

public class PatientProfile
{
    public Guid PatientId { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string BloodGroup { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public string EmergencyContact { get; set; } = string.Empty;

    // Navigation
    public AppUser User { get; set; } = null!;
    public ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
    public ICollection<HealthRecord> HealthRecords { get; set; } = new List<HealthRecord>();
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    public ICollection<AIPrediction> Predictions { get; set; } = new List<AIPrediction>();
    public ICollection<RiskAssessment> RiskAssessments { get; set; } = new List<RiskAssessment>();
    public ICollection<Prescription> Prescriptions { get; set; } = new List<Prescription>();
    public ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();
}

// ─── ASSIGNMENT ───────────────────────────────────────────────────────────────

public class Assignment
{
    public Guid AssignmentId { get; set; } = Guid.NewGuid();
    public Guid DoctorId { get; set; }
    public Guid PatientId { get; set; }
    public DateTime AssignedDate { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    // Navigation
    public DoctorProfile Doctor { get; set; } = null!;
    public PatientProfile Patient { get; set; } = null!;
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}

// ─── APPOINTMENTS ─────────────────────────────────────────────────────────────

public class Appointment
{
    public Guid AppointmentId { get; set; } = Guid.NewGuid();
    public Guid DoctorId { get; set; }
    public Guid PatientId { get; set; }
    public DateTime DateTime { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Booked;
    public UrgencyTier PriorityTier { get; set; } = UrgencyTier.Normal;
    public string? Notes { get; set; }
    public int DurationMinutes { get; set; } = 30;

    // Navigation
    public DoctorProfile Doctor { get; set; } = null!;
    public PatientProfile Patient { get; set; } = null!;
    public RiskAssessment? RiskAssessment { get; set; }
}

// ─── HEALTH RECORDS ───────────────────────────────────────────────────────────

public class HealthRecord
{
    public Guid RecordId { get; set; } = Guid.NewGuid();
    public Guid PatientId { get; set; }
    public Guid DoctorId { get; set; }
    public RecordType RecordType { get; set; }
    public string Data { get; set; } = "{}"; // JSONB
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public PatientProfile Patient { get; set; } = null!;
    public ICollection<ClinicalNote> ClinicalNotes { get; set; } = new List<ClinicalNote>();
    public ICollection<MedicalImage> MedicalImages { get; set; } = new List<MedicalImage>();
    public ICollection<Prescription> Prescriptions { get; set; } = new List<Prescription>();
}

// ─── AI PREDICTIONS ───────────────────────────────────────────────────────────

public class AIPrediction
{
    public Guid PredictionId { get; set; } = Guid.NewGuid();
    public Guid PatientId { get; set; }
    public Guid DoctorId { get; set; }
    public DiseaseType DiseaseType { get; set; }
    public InputModality InputModality { get; set; }
    public string PrimaryDiagnosis { get; set; } = string.Empty;
    public decimal Confidence { get; set; }
    public PredictionStatus Status { get; set; } = PredictionStatus.Pending;
    public bool IsSharedWithPatient { get; set; } = false;
    public string? DoctorInterpretation { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public PatientProfile Patient { get; set; } = null!;
    public DoctorProfile Doctor { get; set; } = null!;
    public ICollection<DifferentialDiagnosis> DifferentialDiagnoses { get; set; } = new List<DifferentialDiagnosis>();
}

public class DifferentialDiagnosis
{
    public Guid DdxId { get; set; } = Guid.NewGuid();
    public Guid PredictionId { get; set; }
    public int RankPosition { get; set; }
    public string Condition { get; set; } = string.Empty;
    public decimal Probability { get; set; }
    public string ReasoningChain { get; set; } = string.Empty;
    public string SuggestedTests { get; set; } = "[]"; // JSON array

    // Navigation
    public AIPrediction Prediction { get; set; } = null!;
}

// ─── RISK ASSESSMENTS ─────────────────────────────────────────────────────────

public class RiskAssessment
{
    public Guid AssessmentId { get; set; } = Guid.NewGuid();
    public Guid PatientId { get; set; }
    public Guid DoctorId { get; set; }
    public decimal RiskScore { get; set; }
    public UrgencyTier UrgencyTier { get; set; }
    public TrendDirection TrendDirection { get; set; } = TrendDirection.Stable;
    public decimal PreviousScore { get; set; }
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public PatientProfile Patient { get; set; } = null!;
    public DoctorProfile Doctor { get; set; } = null!;
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}

// ─── CLINICAL NOTES ───────────────────────────────────────────────────────────

public class ClinicalNote
{
    public Guid NoteId { get; set; } = Guid.NewGuid();
    public Guid HealthRecordId { get; set; }
    public Guid DoctorId { get; set; }
    public string FreeText { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty; // NLP-generated
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public HealthRecord HealthRecord { get; set; } = null!;
    public DoctorProfile Doctor { get; set; } = null!;
    public ICollection<ExtractedEntity> ExtractedEntities { get; set; } = new List<ExtractedEntity>();
}

public class ExtractedEntity
{
    public Guid EntityId { get; set; } = Guid.NewGuid();
    public Guid NoteId { get; set; }
    public string EntityType { get; set; } = string.Empty; // Symptom, Medication, Procedure, Vital
    public string Value { get; set; } = string.Empty;

    // Navigation
    public ClinicalNote Note { get; set; } = null!;
}

// ─── MEDICAL IMAGES ───────────────────────────────────────────────────────────

public class MedicalImage
{
    public Guid ImageId { get; set; } = Guid.NewGuid();
    public Guid PatientId { get; set; }
    public Guid DoctorId { get; set; }
    public Guid? HealthRecordId { get; set; }
    public string Modality { get; set; } = string.Empty; // XRay, MRI, Lesion
    public string FilePath { get; set; } = string.Empty; // MinIO key
    public string AiResult { get; set; } = "{}"; // JSONB with bounding boxes + confidence
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public PatientProfile Patient { get; set; } = null!;
    public DoctorProfile Doctor { get; set; } = null!;
    public HealthRecord? HealthRecord { get; set; }
}

// ─── PRESCRIPTIONS ────────────────────────────────────────────────────────────

public class Prescription
{
    public Guid PrescriptionId { get; set; } = Guid.NewGuid();
    public Guid DoctorId { get; set; }
    public Guid PatientId { get; set; }
    public Guid? HealthRecordId { get; set; }
    public string Medications { get; set; } = "[]"; // JSONB array of medication objects
    public string Status { get; set; } = "Active";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }

    // Navigation
    public DoctorProfile Doctor { get; set; } = null!;
    public PatientProfile Patient { get; set; } = null!;
    public HealthRecord? HealthRecord { get; set; }
}

// ─── CHAT MESSAGES ────────────────────────────────────────────────────────────

public class ChatMessage
{
    public Guid MessageId { get; set; } = Guid.NewGuid();
    public Guid SenderId { get; set; }
    public Guid ReceiverId { get; set; }
    public string Content { get; set; } = string.Empty;
    public ChatMessageType MessageType { get; set; } = ChatMessageType.Text;
    public string? Attachments { get; set; } // JSONB
    public bool IsRead { get; set; } = false;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public AppUser Sender { get; set; } = null!;
    public AppUser Receiver { get; set; } = null!;
}

// ─── FEEDBACK ─────────────────────────────────────────────────────────────────

public class Feedback
{
    public Guid FeedbackId { get; set; } = Guid.NewGuid();
    public Guid PatientId { get; set; }
    public Guid DoctorId { get; set; }
    public int Rating { get; set; } // 1-5
    public string? Comment { get; set; }
    public string? SentimentLabel { get; set; } // Positive, Negative, Neutral
    public double? SentimentScore { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public PatientProfile Patient { get; set; } = null!;
}

// ─── AUDIT LOG ────────────────────────────────────────────────────────────────

public class AuditLog
{
    public long LogId { get; set; } // Auto-increment for append-only
    public Guid UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? OldValues { get; set; } // JSONB
    public string? NewValues { get; set; } // JSONB
    public string? IpAddress { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Navigation
    public AppUser User { get; set; } = null!;
}

// ─── ANNOUNCEMENTS ────────────────────────────────────────────────────────────

public class Announcement
{
    public Guid AnnId { get; set; } = Guid.NewGuid();
    public Guid CreatedBy { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? TargetRole { get; set; } // null = all, "Doctor", "Patient"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }

    // Navigation
    public AppUser Creator { get; set; } = null!;
}

// ─── AI CHATBOT TABLES ────────────────────────────────────────────────────────

public class ChatSession
{
    public Guid SessionId { get; set; } = Guid.NewGuid();
    public Guid DoctorId { get; set; }
    public Guid? PatientContextId { get; set; } // Optional patient being discussed
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public string ContextSummary { get; set; } = string.Empty;

    // Navigation
    public DoctorProfile Doctor { get; set; } = null!;
    public ICollection<ChatbotMessage> Messages { get; set; } = new List<ChatbotMessage>();
    public ICollection<ChatbotMemory> Memories { get; set; } = new List<ChatbotMemory>();
}

public class ChatbotMessage
{
    public Guid MessageId { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public string Role { get; set; } = string.Empty; // "user" or "assistant"
    public InputModality Modality { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? AttachmentUrl { get; set; }
    public string? AiResponse { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Navigation
    public ChatSession Session { get; set; } = null!;
}

public class ChatbotMemory
{
    public Guid MemoryId { get; set; } = Guid.NewGuid();
    public Guid DoctorId { get; set; }
    public Guid? SessionId { get; set; }
    public string Embedding { get; set; } = "[]"; // pgvector stored as text, cast in queries
    public string ContextType { get; set; } = string.Empty; // "patient_history", "guideline", "note"
    public string SourceRef { get; set; } = string.Empty;

    // Navigation
    public ChatSession? Session { get; set; }
}
