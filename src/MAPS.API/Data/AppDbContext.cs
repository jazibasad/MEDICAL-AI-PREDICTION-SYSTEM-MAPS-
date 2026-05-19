using Microsoft.EntityFrameworkCore;
using MAPS.API.Data.Entities;
using MAPS.Shared.Enums;

namespace MAPS.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // ─── DbSets — All 20 Tables ───────────────────────────────────────────────
    public DbSet<AppUser>              Users                 { get; set; }
    public DbSet<DoctorProfile>        DoctorProfiles        { get; set; }
    public DbSet<PatientProfile>       PatientProfiles       { get; set; }
    public DbSet<Assignment>           Assignments           { get; set; }
    public DbSet<Appointment>          Appointments          { get; set; }
    public DbSet<HealthRecord>         HealthRecords         { get; set; }
    public DbSet<AIPrediction>         AIPredictions         { get; set; }
    public DbSet<DifferentialDiagnosis> DifferentialDiagnoses { get; set; }
    public DbSet<RiskAssessment>       RiskAssessments       { get; set; }
    public DbSet<ClinicalNote>         ClinicalNotes         { get; set; }
    public DbSet<ExtractedEntity>      ExtractedEntities     { get; set; }
    public DbSet<MedicalImage>         MedicalImages         { get; set; }
    public DbSet<Prescription>         Prescriptions         { get; set; }
    public DbSet<ChatMessage>          ChatMessages          { get; set; }
    public DbSet<Feedback>             Feedbacks             { get; set; }
    public DbSet<AuditLog>             AuditLogs             { get; set; }
    public DbSet<Announcement>         Announcements         { get; set; }
    public DbSet<ChatSession>          ChatSessions          { get; set; }
    public DbSet<ChatbotMessage>       ChatbotMessages       { get; set; }
    public DbSet<ChatbotMemory>        ChatbotMemories       { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Enable pgvector extension
        modelBuilder.HasPostgresExtension("vector");

        // ─── AppUser ──────────────────────────────────────────────────────────
        modelBuilder.Entity<AppUser>(e =>
        {
            e.ToTable("Users");
            e.HasKey(u => u.UserId);
            e.Property(u => u.Email).IsRequired().HasMaxLength(256);
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.FullName).IsRequired().HasMaxLength(200);
            e.Property(u => u.PasswordHash).IsRequired();
            e.Property(u => u.Role).HasConversion<int>();
            e.Property(u => u.CreatedAt).HasDefaultValueSql("NOW()");

            e.HasOne(u => u.DoctorProfile)
             .WithOne(d => d.User)
             .HasForeignKey<DoctorProfile>(d => d.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(u => u.PatientProfile)
             .WithOne(p => p.User)
             .HasForeignKey<PatientProfile>(p => p.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            // Self-referencing chat messages
            e.HasMany(u => u.SentMessages)
             .WithOne(m => m.Sender)
             .HasForeignKey(m => m.SenderId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(u => u.ReceivedMessages)
             .WithOne(m => m.Receiver)
             .HasForeignKey(m => m.ReceiverId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ─── DoctorProfile ────────────────────────────────────────────────────
        modelBuilder.Entity<DoctorProfile>(e =>
        {
            e.ToTable("DoctorProfiles");
            e.HasKey(d => d.DoctorId);
            e.Property(d => d.Specialization).HasMaxLength(150);
            e.Property(d => d.LicenseNumber).HasMaxLength(100);
            e.Property(d => d.Department).HasMaxLength(150);
            e.HasIndex(d => d.LicenseNumber).IsUnique();
        });

        // ─── PatientProfile ───────────────────────────────────────────────────
        modelBuilder.Entity<PatientProfile>(e =>
        {
            e.ToTable("PatientProfiles");
            e.HasKey(p => p.PatientId);
            e.Property(p => p.BloodGroup).HasMaxLength(10);
            e.Property(p => p.EmergencyContact).HasMaxLength(200);
        });

        // ─── Assignment ───────────────────────────────────────────────────────
        modelBuilder.Entity<Assignment>(e =>
        {
            e.ToTable("Assignments");
            e.HasKey(a => a.AssignmentId);
            e.Property(a => a.AssignedDate).HasDefaultValueSql("NOW()");

            e.HasOne(a => a.Doctor)
             .WithMany(d => d.Assignments)
             .HasForeignKey(a => a.DoctorId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(a => a.Patient)
             .WithMany(p => p.Assignments)
             .HasForeignKey(a => a.PatientId)
             .OnDelete(DeleteBehavior.Restrict);

            // Prevent duplicate active assignments
            e.HasIndex(a => new { a.DoctorId, a.PatientId, a.IsActive }).IsUnique();
        });

        // ─── Appointment ──────────────────────────────────────────────────────
        modelBuilder.Entity<Appointment>(e =>
        {
            e.ToTable("Appointments");
            e.HasKey(a => a.AppointmentId);
            e.Property(a => a.Status).HasConversion<int>();
            e.Property(a => a.PriorityTier).HasConversion<int>();

            e.HasOne(a => a.Doctor)
             .WithMany()
             .HasForeignKey(a => a.DoctorId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(a => a.Patient)
             .WithMany(p => p.Appointments)
             .HasForeignKey(a => a.PatientId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(a => a.RiskAssessment)
             .WithMany(r => r.Appointments)
             .HasForeignKey(a => a.AppointmentId)
             .IsRequired(false)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ─── HealthRecord ─────────────────────────────────────────────────────
        modelBuilder.Entity<HealthRecord>(e =>
        {
            e.ToTable("HealthRecords");
            e.HasKey(h => h.RecordId);
            e.Property(h => h.RecordType).HasConversion<int>();
            e.Property(h => h.Data).HasColumnType("jsonb");
            e.Property(h => h.CreatedAt).HasDefaultValueSql("NOW()");

            e.HasOne(h => h.Patient)
             .WithMany(p => p.HealthRecords)
             .HasForeignKey(h => h.PatientId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ─── AIPrediction ─────────────────────────────────────────────────────
        modelBuilder.Entity<AIPrediction>(e =>
        {
            e.ToTable("AIPredictions");
            e.HasKey(p => p.PredictionId);
            e.Property(p => p.DiseaseType).HasConversion<int>();
            e.Property(p => p.InputModality).HasConversion<int>();
            e.Property(p => p.Status).HasConversion<int>();
            e.Property(p => p.Confidence).HasPrecision(5, 4);
            e.Property(p => p.CreatedAt).HasDefaultValueSql("NOW()");

            e.HasOne(p => p.Patient)
             .WithMany(pt => pt.Predictions)
             .HasForeignKey(p => p.PatientId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(p => p.Doctor)
             .WithMany(d => d.Predictions)
             .HasForeignKey(p => p.DoctorId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ─── DifferentialDiagnosis ────────────────────────────────────────────
        modelBuilder.Entity<DifferentialDiagnosis>(e =>
        {
            e.ToTable("DifferentialDiagnoses");
            e.HasKey(d => d.DdxId);
            e.Property(d => d.Probability).HasPrecision(5, 4);
            e.Property(d => d.SuggestedTests).HasColumnType("jsonb");

            e.HasOne(d => d.Prediction)
             .WithMany(p => p.DifferentialDiagnoses)
             .HasForeignKey(d => d.PredictionId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── RiskAssessment ───────────────────────────────────────────────────
        modelBuilder.Entity<RiskAssessment>(e =>
        {
            e.ToTable("RiskAssessments");
            e.HasKey(r => r.AssessmentId);
            e.Property(r => r.UrgencyTier).HasConversion<int>();
            e.Property(r => r.TrendDirection).HasConversion<int>();
            e.Property(r => r.RiskScore).HasPrecision(5, 2);
            e.Property(r => r.PreviousScore).HasPrecision(5, 2);
            e.Property(r => r.CalculatedAt).HasDefaultValueSql("NOW()");

            e.HasOne(r => r.Patient)
             .WithMany(p => p.RiskAssessments)
             .HasForeignKey(r => r.PatientId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(r => r.Doctor)
             .WithMany(d => d.RiskAssessments)
             .HasForeignKey(r => r.DoctorId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ─── ClinicalNote ─────────────────────────────────────────────────────
        modelBuilder.Entity<ClinicalNote>(e =>
        {
            e.ToTable("ClinicalNotes");
            e.HasKey(n => n.NoteId);
            e.Property(n => n.CreatedAt).HasDefaultValueSql("NOW()");

            e.HasOne(n => n.HealthRecord)
             .WithMany(h => h.ClinicalNotes)
             .HasForeignKey(n => n.HealthRecordId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(n => n.Doctor)
             .WithMany(d => d.ClinicalNotes)
             .HasForeignKey(n => n.DoctorId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ─── ExtractedEntity ──────────────────────────────────────────────────
        modelBuilder.Entity<ExtractedEntity>(e =>
        {
            e.ToTable("ExtractedEntities");
            e.HasKey(x => x.EntityId);
            e.Property(x => x.EntityType).HasMaxLength(50);
            e.Property(x => x.Value).HasMaxLength(500);

            e.HasOne(x => x.Note)
             .WithMany(n => n.ExtractedEntities)
             .HasForeignKey(x => x.NoteId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── MedicalImage ─────────────────────────────────────────────────────
        modelBuilder.Entity<MedicalImage>(e =>
        {
            e.ToTable("MedicalImages");
            e.HasKey(i => i.ImageId);
            e.Property(i => i.AiResult).HasColumnType("jsonb");
            e.Property(i => i.UploadedAt).HasDefaultValueSql("NOW()");

            e.HasOne(i => i.Patient)
             .WithMany()
             .HasForeignKey(i => i.PatientId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(i => i.Doctor)
             .WithMany(d => d.MedicalImages)
             .HasForeignKey(i => i.DoctorId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(i => i.HealthRecord)
             .WithMany(h => h.MedicalImages)
             .HasForeignKey(i => i.HealthRecordId)
             .IsRequired(false)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ─── Prescription ─────────────────────────────────────────────────────
        modelBuilder.Entity<Prescription>(e =>
        {
            e.ToTable("Prescriptions");
            e.HasKey(p => p.PrescriptionId);
            e.Property(p => p.Medications).HasColumnType("jsonb");
            e.Property(p => p.CreatedAt).HasDefaultValueSql("NOW()");

            e.HasOne(p => p.Doctor)
             .WithMany(d => d.Prescriptions)
             .HasForeignKey(p => p.DoctorId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(p => p.Patient)
             .WithMany(pt => pt.Prescriptions)
             .HasForeignKey(p => p.PatientId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(p => p.HealthRecord)
             .WithMany(h => h.Prescriptions)
             .HasForeignKey(p => p.HealthRecordId)
             .IsRequired(false)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ─── ChatMessage ──────────────────────────────────────────────────────
        modelBuilder.Entity<ChatMessage>(e =>
        {
            e.ToTable("ChatMessages");
            e.HasKey(m => m.MessageId);
            e.Property(m => m.MessageType).HasConversion<int>();
            e.Property(m => m.Attachments).HasColumnType("jsonb");
            e.Property(m => m.SentAt).HasDefaultValueSql("NOW()");
        });

        // ─── Feedback ─────────────────────────────────────────────────────────
        modelBuilder.Entity<Feedback>(e =>
        {
            e.ToTable("Feedbacks");
            e.HasKey(f => f.FeedbackId);
            e.Property(f => f.Rating).HasAnnotation("Range", new[] { 1, 5 });
            e.Property(f => f.SubmittedAt).HasDefaultValueSql("NOW()");

            e.HasOne(f => f.Patient)
             .WithMany(p => p.Feedbacks)
             .HasForeignKey(f => f.PatientId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ─── AuditLog — Append-Only ───────────────────────────────────────────
        modelBuilder.Entity<AuditLog>(e =>
        {
            e.ToTable("AuditLogs");
            e.HasKey(a => a.LogId);
            e.Property(a => a.LogId).UseIdentityAlwaysColumn(); // PostgreSQL identity
            e.Property(a => a.OldValues).HasColumnType("jsonb");
            e.Property(a => a.NewValues).HasColumnType("jsonb");
            e.Property(a => a.Timestamp).HasDefaultValueSql("NOW()");
            e.Property(a => a.Action).HasMaxLength(200);
            e.Property(a => a.EntityType).HasMaxLength(100);

            e.HasOne(a => a.User)
             .WithMany(u => u.AuditLogs)
             .HasForeignKey(a => a.UserId)
             .OnDelete(DeleteBehavior.Restrict);

            // Index for fast lookup by user and time
            e.HasIndex(a => new { a.UserId, a.Timestamp });
            e.HasIndex(a => a.Timestamp);
        });

        // ─── Announcement ─────────────────────────────────────────────────────
        modelBuilder.Entity<Announcement>(e =>
        {
            e.ToTable("Announcements");
            e.HasKey(a => a.AnnId);
            e.Property(a => a.Title).HasMaxLength(300);
            e.Property(a => a.CreatedAt).HasDefaultValueSql("NOW()");

            e.HasOne(a => a.Creator)
             .WithMany(u => u.CreatedAnnouncements)
             .HasForeignKey(a => a.CreatedBy)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ─── ChatSession ──────────────────────────────────────────────────────
        modelBuilder.Entity<ChatSession>(e =>
        {
            e.ToTable("ChatSessions");
            e.HasKey(s => s.SessionId);
            e.Property(s => s.StartedAt).HasDefaultValueSql("NOW()");

            e.HasOne(s => s.Doctor)
             .WithMany(d => d.ChatSessions)
             .HasForeignKey(s => s.DoctorId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ─── ChatbotMessage ───────────────────────────────────────────────────
        modelBuilder.Entity<ChatbotMessage>(e =>
        {
            e.ToTable("ChatbotMessages");
            e.HasKey(m => m.MessageId);
            e.Property(m => m.Modality).HasConversion<int>();
            e.Property(m => m.Role).HasMaxLength(20);
            e.Property(m => m.Timestamp).HasDefaultValueSql("NOW()");

            e.HasOne(m => m.Session)
             .WithMany(s => s.Messages)
             .HasForeignKey(m => m.SessionId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── ChatbotMemory — pgvector ─────────────────────────────────────────
        modelBuilder.Entity<ChatbotMemory>(e =>
        {
            e.ToTable("ChatbotMemories");
            e.HasKey(m => m.MemoryId);
            e.Property(m => m.ContextType).HasMaxLength(100);
            e.Property(m => m.SourceRef).HasMaxLength(500);

            // Embedding stored as vector(1536) — raw SQL for pgvector
            e.Property(m => m.Embedding)
             .HasColumnType("vector(1536)");

            e.HasOne(m => m.Session)
             .WithMany(s => s.Memories)
             .HasForeignKey(m => m.SessionId)
             .IsRequired(false)
             .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
