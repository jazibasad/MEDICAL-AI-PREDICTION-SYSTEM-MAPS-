using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MAPS.API.Data.Migrations;

public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Enable pgvector extension
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS vector;");

        // ─── Users ────────────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "Users",
            columns: table => new
            {
                UserId           = table.Column<Guid>(nullable: false, defaultValueSql: "gen_random_uuid()"),
                FullName         = table.Column<string>(maxLength: 200, nullable: false),
                Email            = table.Column<string>(maxLength: 256, nullable: false),
                PasswordHash     = table.Column<string>(nullable: false),
                Role             = table.Column<int>(nullable: false),
                IsActive         = table.Column<bool>(nullable: false, defaultValue: true),
                IsApproved       = table.Column<bool>(nullable: false, defaultValue: false),
                RefreshToken     = table.Column<string>(nullable: true),
                RefreshTokenExpiry = table.Column<DateTime>(nullable: true),
                CreatedAt        = table.Column<DateTime>(nullable: false, defaultValueSql: "NOW()")
            },
            constraints: table => table.PrimaryKey("PK_Users", x => x.UserId));

        migrationBuilder.CreateIndex("IX_Users_Email", "Users", "Email", unique: true);

        // ─── DoctorProfiles ───────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "DoctorProfiles",
            columns: table => new
            {
                DoctorId       = table.Column<Guid>(nullable: false, defaultValueSql: "gen_random_uuid()"),
                UserId         = table.Column<Guid>(nullable: false),
                Specialization = table.Column<string>(maxLength: 150, nullable: false),
                LicenseNumber  = table.Column<string>(maxLength: 100, nullable: false),
                Department     = table.Column<string>(maxLength: 150, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DoctorProfiles", x => x.DoctorId);
                table.ForeignKey("FK_DoctorProfiles_Users", x => x.UserId, "Users", "UserId", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("IX_DoctorProfiles_LicenseNumber", "DoctorProfiles", "LicenseNumber", unique: true);
        migrationBuilder.CreateIndex("IX_DoctorProfiles_UserId", "DoctorProfiles", "UserId", unique: true);

        // ─── PatientProfiles ──────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "PatientProfiles",
            columns: table => new
            {
                PatientId        = table.Column<Guid>(nullable: false, defaultValueSql: "gen_random_uuid()"),
                UserId           = table.Column<Guid>(nullable: false),
                BloodGroup       = table.Column<string>(maxLength: 10, nullable: false),
                DateOfBirth      = table.Column<DateTime>(nullable: true),
                EmergencyContact = table.Column<string>(maxLength: 200, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PatientProfiles", x => x.PatientId);
                table.ForeignKey("FK_PatientProfiles_Users", x => x.UserId, "Users", "UserId", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("IX_PatientProfiles_UserId", "PatientProfiles", "UserId", unique: true);

        // ─── Assignments ──────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "Assignments",
            columns: table => new
            {
                AssignmentId = table.Column<Guid>(nullable: false, defaultValueSql: "gen_random_uuid()"),
                DoctorId     = table.Column<Guid>(nullable: false),
                PatientId    = table.Column<Guid>(nullable: false),
                AssignedDate = table.Column<DateTime>(nullable: false, defaultValueSql: "NOW()"),
                IsActive     = table.Column<bool>(nullable: false, defaultValue: true),
                Notes        = table.Column<string>(nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Assignments", x => x.AssignmentId);
                table.ForeignKey("FK_Assignments_Doctors", x => x.DoctorId, "DoctorProfiles", "DoctorId", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_Assignments_Patients", x => x.PatientId, "PatientProfiles", "PatientId", onDelete: ReferentialAction.Restrict);
            });

        // ─── Appointments ─────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "Appointments",
            columns: table => new
            {
                AppointmentId   = table.Column<Guid>(nullable: false, defaultValueSql: "gen_random_uuid()"),
                DoctorId        = table.Column<Guid>(nullable: false),
                PatientId       = table.Column<Guid>(nullable: false),
                DateTime        = table.Column<DateTime>(nullable: false),
                Status          = table.Column<int>(nullable: false, defaultValue: 1),
                PriorityTier    = table.Column<int>(nullable: false, defaultValue: 3),
                Notes           = table.Column<string>(nullable: true),
                DurationMinutes = table.Column<int>(nullable: false, defaultValue: 30)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Appointments", x => x.AppointmentId);
                table.ForeignKey("FK_Appointments_Doctors", x => x.DoctorId, "DoctorProfiles", "DoctorId", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_Appointments_Patients", x => x.PatientId, "PatientProfiles", "PatientId", onDelete: ReferentialAction.Restrict);
            });

        // ─── HealthRecords ────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "HealthRecords",
            columns: table => new
            {
                RecordId   = table.Column<Guid>(nullable: false, defaultValueSql: "gen_random_uuid()"),
                PatientId  = table.Column<Guid>(nullable: false),
                DoctorId   = table.Column<Guid>(nullable: false),
                RecordType = table.Column<int>(nullable: false),
                Data       = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                CreatedAt  = table.Column<DateTime>(nullable: false, defaultValueSql: "NOW()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_HealthRecords", x => x.RecordId);
                table.ForeignKey("FK_HealthRecords_Patients", x => x.PatientId, "PatientProfiles", "PatientId", onDelete: ReferentialAction.Restrict);
            });

        // ─── AIPredictions ────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "AIPredictions",
            columns: table => new
            {
                PredictionId        = table.Column<Guid>(nullable: false, defaultValueSql: "gen_random_uuid()"),
                PatientId           = table.Column<Guid>(nullable: false),
                DoctorId            = table.Column<Guid>(nullable: false),
                DiseaseType         = table.Column<int>(nullable: false),
                InputModality       = table.Column<int>(nullable: false),
                PrimaryDiagnosis    = table.Column<string>(nullable: false),
                Confidence          = table.Column<decimal>(precision: 5, scale: 4, nullable: false),
                Status              = table.Column<int>(nullable: false, defaultValue: 1),
                IsSharedWithPatient = table.Column<bool>(nullable: false, defaultValue: false),
                DoctorInterpretation = table.Column<string>(nullable: true),
                CreatedAt           = table.Column<DateTime>(nullable: false, defaultValueSql: "NOW()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AIPredictions", x => x.PredictionId);
                table.ForeignKey("FK_AIPredictions_Patients", x => x.PatientId, "PatientProfiles", "PatientId", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_AIPredictions_Doctors", x => x.DoctorId, "DoctorProfiles", "DoctorId", onDelete: ReferentialAction.Restrict);
            });

        // ─── DifferentialDiagnoses ────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "DifferentialDiagnoses",
            columns: table => new
            {
                DdxId          = table.Column<Guid>(nullable: false, defaultValueSql: "gen_random_uuid()"),
                PredictionId   = table.Column<Guid>(nullable: false),
                RankPosition   = table.Column<int>(nullable: false),
                Condition      = table.Column<string>(nullable: false),
                Probability    = table.Column<decimal>(precision: 5, scale: 4, nullable: false),
                ReasoningChain = table.Column<string>(nullable: false),
                SuggestedTests = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DifferentialDiagnoses", x => x.DdxId);
                table.ForeignKey("FK_DifferentialDiagnoses_Predictions", x => x.PredictionId, "AIPredictions", "PredictionId", onDelete: ReferentialAction.Cascade);
            });

        // ─── RiskAssessments ──────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "RiskAssessments",
            columns: table => new
            {
                AssessmentId   = table.Column<Guid>(nullable: false, defaultValueSql: "gen_random_uuid()"),
                PatientId      = table.Column<Guid>(nullable: false),
                DoctorId       = table.Column<Guid>(nullable: false),
                RiskScore      = table.Column<decimal>(precision: 5, scale: 2, nullable: false),
                UrgencyTier    = table.Column<int>(nullable: false),
                TrendDirection = table.Column<int>(nullable: false, defaultValue: 2),
                PreviousScore  = table.Column<decimal>(precision: 5, scale: 2, nullable: false),
                CalculatedAt   = table.Column<DateTime>(nullable: false, defaultValueSql: "NOW()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RiskAssessments", x => x.AssessmentId);
                table.ForeignKey("FK_RiskAssessments_Patients", x => x.PatientId, "PatientProfiles", "PatientId", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_RiskAssessments_Doctors", x => x.DoctorId, "DoctorProfiles", "DoctorId", onDelete: ReferentialAction.Restrict);
            });

        // ─── ClinicalNotes ────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "ClinicalNotes",
            columns: table => new
            {
                NoteId          = table.Column<Guid>(nullable: false, defaultValueSql: "gen_random_uuid()"),
                HealthRecordId  = table.Column<Guid>(nullable: false),
                DoctorId        = table.Column<Guid>(nullable: false),
                FreeText        = table.Column<string>(nullable: false),
                Summary         = table.Column<string>(nullable: false, defaultValue: ""),
                CreatedAt       = table.Column<DateTime>(nullable: false, defaultValueSql: "NOW()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ClinicalNotes", x => x.NoteId);
                table.ForeignKey("FK_ClinicalNotes_HealthRecords", x => x.HealthRecordId, "HealthRecords", "RecordId", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ClinicalNotes_Doctors", x => x.DoctorId, "DoctorProfiles", "DoctorId", onDelete: ReferentialAction.Restrict);
            });

        // ─── ExtractedEntities ────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "ExtractedEntities",
            columns: table => new
            {
                EntityId   = table.Column<Guid>(nullable: false, defaultValueSql: "gen_random_uuid()"),
                NoteId     = table.Column<Guid>(nullable: false),
                EntityType = table.Column<string>(maxLength: 50, nullable: false),
                Value      = table.Column<string>(maxLength: 500, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ExtractedEntities", x => x.EntityId);
                table.ForeignKey("FK_ExtractedEntities_ClinicalNotes", x => x.NoteId, "ClinicalNotes", "NoteId", onDelete: ReferentialAction.Cascade);
            });

        // ─── MedicalImages ────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "MedicalImages",
            columns: table => new
            {
                ImageId        = table.Column<Guid>(nullable: false, defaultValueSql: "gen_random_uuid()"),
                PatientId      = table.Column<Guid>(nullable: false),
                DoctorId       = table.Column<Guid>(nullable: false),
                HealthRecordId = table.Column<Guid>(nullable: true),
                Modality       = table.Column<string>(nullable: false),
                FilePath       = table.Column<string>(nullable: false),
                AiResult       = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                UploadedAt     = table.Column<DateTime>(nullable: false, defaultValueSql: "NOW()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MedicalImages", x => x.ImageId);
                table.ForeignKey("FK_MedicalImages_Patients", x => x.PatientId, "PatientProfiles", "PatientId", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_MedicalImages_Doctors", x => x.DoctorId, "DoctorProfiles", "DoctorId", onDelete: ReferentialAction.Restrict);
            });

        // ─── Prescriptions ────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "Prescriptions",
            columns: table => new
            {
                PrescriptionId = table.Column<Guid>(nullable: false, defaultValueSql: "gen_random_uuid()"),
                DoctorId       = table.Column<Guid>(nullable: false),
                PatientId      = table.Column<Guid>(nullable: false),
                HealthRecordId = table.Column<Guid>(nullable: true),
                Medications    = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                Status         = table.Column<string>(nullable: false, defaultValue: "Active"),
                CreatedAt      = table.Column<DateTime>(nullable: false, defaultValueSql: "NOW()"),
                ExpiresAt      = table.Column<DateTime>(nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Prescriptions", x => x.PrescriptionId);
                table.ForeignKey("FK_Prescriptions_Doctors", x => x.DoctorId, "DoctorProfiles", "DoctorId", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_Prescriptions_Patients", x => x.PatientId, "PatientProfiles", "PatientId", onDelete: ReferentialAction.Restrict);
            });

        // ─── ChatMessages ─────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "ChatMessages",
            columns: table => new
            {
                MessageId   = table.Column<Guid>(nullable: false, defaultValueSql: "gen_random_uuid()"),
                SenderId    = table.Column<Guid>(nullable: false),
                ReceiverId  = table.Column<Guid>(nullable: false),
                Content     = table.Column<string>(nullable: false),
                MessageType = table.Column<int>(nullable: false, defaultValue: 1),
                Attachments = table.Column<string>(type: "jsonb", nullable: true),
                IsRead      = table.Column<bool>(nullable: false, defaultValue: false),
                SentAt      = table.Column<DateTime>(nullable: false, defaultValueSql: "NOW()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ChatMessages", x => x.MessageId);
                table.ForeignKey("FK_ChatMessages_Sender", x => x.SenderId, "Users", "UserId", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ChatMessages_Receiver", x => x.ReceiverId, "Users", "UserId", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_ChatMessages_Participants", "ChatMessages",
            new[] { "SenderId", "ReceiverId", "SentAt" });

        // ─── Feedbacks ────────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "Feedbacks",
            columns: table => new
            {
                FeedbackId     = table.Column<Guid>(nullable: false, defaultValueSql: "gen_random_uuid()"),
                PatientId      = table.Column<Guid>(nullable: false),
                DoctorId       = table.Column<Guid>(nullable: false),
                Rating         = table.Column<int>(nullable: false),
                Comment        = table.Column<string>(nullable: true),
                SentimentLabel = table.Column<string>(nullable: true),
                SentimentScore = table.Column<double>(nullable: true),
                SubmittedAt    = table.Column<DateTime>(nullable: false, defaultValueSql: "NOW()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Feedbacks", x => x.FeedbackId);
                table.ForeignKey("FK_Feedbacks_Patients", x => x.PatientId, "PatientProfiles", "PatientId", onDelete: ReferentialAction.Restrict);
            });

        // ─── AuditLogs ────────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "AuditLogs",
            columns: table => new
            {
                LogId      = table.Column<long>(nullable: false).Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                UserId     = table.Column<Guid>(nullable: false),
                Action     = table.Column<string>(maxLength: 200, nullable: false),
                EntityType = table.Column<string>(maxLength: 100, nullable: false),
                EntityId   = table.Column<string>(nullable: true),
                OldValues  = table.Column<string>(type: "jsonb", nullable: true),
                NewValues  = table.Column<string>(type: "jsonb", nullable: true),
                IpAddress  = table.Column<string>(nullable: true),
                Timestamp  = table.Column<DateTime>(nullable: false, defaultValueSql: "NOW()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuditLogs", x => x.LogId);
                table.ForeignKey("FK_AuditLogs_Users", x => x.UserId, "Users", "UserId", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_AuditLogs_UserId_Timestamp", "AuditLogs", new[] { "UserId", "Timestamp" });
        migrationBuilder.CreateIndex("IX_AuditLogs_Timestamp", "AuditLogs", "Timestamp");

        // ─── Announcements ────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "Announcements",
            columns: table => new
            {
                AnnId      = table.Column<Guid>(nullable: false, defaultValueSql: "gen_random_uuid()"),
                CreatedBy  = table.Column<Guid>(nullable: false),
                Title      = table.Column<string>(maxLength: 300, nullable: false),
                Content    = table.Column<string>(nullable: false),
                TargetRole = table.Column<string>(nullable: true),
                CreatedAt  = table.Column<DateTime>(nullable: false, defaultValueSql: "NOW()"),
                ExpiresAt  = table.Column<DateTime>(nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Announcements", x => x.AnnId);
                table.ForeignKey("FK_Announcements_Users", x => x.CreatedBy, "Users", "UserId", onDelete: ReferentialAction.Restrict);
            });

        // ─── ChatSessions ─────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "ChatSessions",
            columns: table => new
            {
                SessionId        = table.Column<Guid>(nullable: false, defaultValueSql: "gen_random_uuid()"),
                DoctorId         = table.Column<Guid>(nullable: false),
                PatientContextId = table.Column<Guid>(nullable: true),
                StartedAt        = table.Column<DateTime>(nullable: false, defaultValueSql: "NOW()"),
                ContextSummary   = table.Column<string>(nullable: false, defaultValue: "")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ChatSessions", x => x.SessionId);
                table.ForeignKey("FK_ChatSessions_Doctors", x => x.DoctorId, "DoctorProfiles", "DoctorId", onDelete: ReferentialAction.Restrict);
            });

        // ─── ChatbotMessages ──────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "ChatbotMessages",
            columns: table => new
            {
                MessageId     = table.Column<Guid>(nullable: false, defaultValueSql: "gen_random_uuid()"),
                SessionId     = table.Column<Guid>(nullable: false),
                Role          = table.Column<string>(maxLength: 20, nullable: false),
                Modality      = table.Column<int>(nullable: false, defaultValue: 1),
                Content       = table.Column<string>(nullable: false),
                AttachmentUrl = table.Column<string>(nullable: true),
                AiResponse    = table.Column<string>(nullable: true),
                Timestamp     = table.Column<DateTime>(nullable: false, defaultValueSql: "NOW()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ChatbotMessages", x => x.MessageId);
                table.ForeignKey("FK_ChatbotMessages_Sessions", x => x.SessionId, "ChatSessions", "SessionId", onDelete: ReferentialAction.Cascade);
            });

        // ─── ChatbotMemories — pgvector ───────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "ChatbotMemories",
            columns: table => new
            {
                MemoryId    = table.Column<Guid>(nullable: false, defaultValueSql: "gen_random_uuid()"),
                DoctorId    = table.Column<Guid>(nullable: false),
                SessionId   = table.Column<Guid>(nullable: true),
                Embedding   = table.Column<string>(type: "vector(1536)", nullable: false),
                ContextType = table.Column<string>(maxLength: 100, nullable: false),
                SourceRef   = table.Column<string>(maxLength: 500, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ChatbotMemories", x => x.MemoryId);
                table.ForeignKey("FK_ChatbotMemories_Sessions", x => x.SessionId, "ChatSessions", "SessionId", onDelete: ReferentialAction.SetNull);
            });

        // IVFFlat index for fast pgvector similarity search
        migrationBuilder.Sql(
            "CREATE INDEX IF NOT EXISTS IX_ChatbotMemories_Embedding " +
            "ON \"ChatbotMemories\" USING ivfflat (\"Embedding\" vector_cosine_ops) WITH (lists = 100);");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("ChatbotMemories");
        migrationBuilder.DropTable("ChatbotMessages");
        migrationBuilder.DropTable("ChatSessions");
        migrationBuilder.DropTable("Announcements");
        migrationBuilder.DropTable("AuditLogs");
        migrationBuilder.DropTable("Feedbacks");
        migrationBuilder.DropTable("ChatMessages");
        migrationBuilder.DropTable("Prescriptions");
        migrationBuilder.DropTable("MedicalImages");
        migrationBuilder.DropTable("ExtractedEntities");
        migrationBuilder.DropTable("ClinicalNotes");
        migrationBuilder.DropTable("RiskAssessments");
        migrationBuilder.DropTable("DifferentialDiagnoses");
        migrationBuilder.DropTable("AIPredictions");
        migrationBuilder.DropTable("HealthRecords");
        migrationBuilder.DropTable("Appointments");
        migrationBuilder.DropTable("Assignments");
        migrationBuilder.DropTable("PatientProfiles");
        migrationBuilder.DropTable("DoctorProfiles");
        migrationBuilder.DropTable("Users");
        migrationBuilder.Sql("DROP EXTENSION IF EXISTS vector;");
    }
}
