using Microsoft.EntityFrameworkCore;
using MAPS.API.Data;
using MAPS.API.Data.Entities;
using MAPS.API.Data.Repositories.Interfaces;
using MAPS.ML.NLP;
using MAPS.Shared.DTOs.Common;

namespace MAPS.API.Services.NLP;

public class ClinicalNoteDto
{
    public Guid     NoteId          { get; set; }
    public Guid     HealthRecordId  { get; set; }
    public string   FreeText        { get; set; } = string.Empty;
    public string   Summary         { get; set; } = string.Empty;
    public string   Sentiment       { get; set; } = string.Empty;
    public List<EntityDto> Entities { get; set; } = new();
    public DateTime CreatedAt       { get; set; }
}

public class EntityDto
{
    public string EntityType { get; set; } = string.Empty;
    public string Value      { get; set; } = string.Empty;
    public float  Confidence { get; set; }
}

public class CreateNoteRequest
{
    public Guid   PatientId { get; set; }
    public string FreeText  { get; set; } = string.Empty;
}

public interface INlpService
{
    Task<ApiResponse<ClinicalNoteDto>> CreateNoteAsync(
        CreateNoteRequest req, Guid doctorId);
    Task<ApiResponse<List<ClinicalNoteDto>>> GetNotesByPatientAsync(
        Guid patientId, Guid doctorId);
    Task<ApiResponse<List<EntityDto>>> GetEntitiesAsync(Guid noteId, Guid doctorId);
}

public class NlpService : INlpService
{
    private readonly AppDbContext          _context;
    private readonly IAssignmentRepository _assignRepo;
    private readonly IAuditRepository      _auditRepo;
    private readonly IClinicalNlpPipeline  _nlpPipeline;
    private readonly ILogger<NlpService>   _logger;

    public NlpService(
        AppDbContext          context,
        IAssignmentRepository assignRepo,
        IAuditRepository      auditRepo,
        IClinicalNlpPipeline  nlpPipeline,
        ILogger<NlpService>   logger)
    {
        _context     = context;
        _assignRepo  = assignRepo;
        _auditRepo   = auditRepo;
        _nlpPipeline = nlpPipeline;
        _logger      = logger;
    }

    public async Task<ApiResponse<ClinicalNoteDto>> CreateNoteAsync(
        CreateNoteRequest req, Guid doctorId)
    {
        if (!await _assignRepo.IsPatientAssignedToDoctor(req.PatientId, doctorId))
            return ApiResponse<ClinicalNoteDto>.Fail("Patient not assigned to you.");

        if (string.IsNullOrWhiteSpace(req.FreeText))
            return ApiResponse<ClinicalNoteDto>.Fail("Note text cannot be empty.");

        // Run NLP pipeline
        var nlpResult = _nlpPipeline.Process(req.FreeText);
        _logger.LogInformation(
            "NLP processed note: {Entities} entities, sentiment={Sentiment}",
            nlpResult.Entities.Count, nlpResult.Sentiment);

        // Get or create health record for today
        var doctorProfile = await _context.DoctorProfiles
            .FirstOrDefaultAsync(d => d.DoctorId == doctorId);

        var healthRecord = new HealthRecord
        {
            PatientId  = req.PatientId,
            DoctorId   = doctorId,
            RecordType = MAPS.Shared.Enums.RecordType.Note,
            Data       = $"{{\"sentiment\":\"{nlpResult.Sentiment}\"}}",
            CreatedAt  = DateTime.UtcNow
        };
        _context.HealthRecords.Add(healthRecord);
        await _context.SaveChangesAsync();

        // Create clinical note
        var note = new ClinicalNote
        {
            HealthRecordId = healthRecord.RecordId,
            DoctorId       = doctorId,
            FreeText       = req.FreeText,
            Summary        = nlpResult.Summary,
            CreatedAt      = DateTime.UtcNow
        };
        _context.ClinicalNotes.Add(note);
        await _context.SaveChangesAsync();

        // Persist extracted entities
        var entities = nlpResult.Entities.Select(e => new ExtractedEntity
        {
            NoteId     = note.NoteId,
            EntityType = e.EntityType,
            Value      = e.Value
        }).ToList();

        _context.ExtractedEntities.AddRange(entities);
        await _context.SaveChangesAsync();

        await _auditRepo.LogAsync(new AuditLog
        {
            UserId     = doctorId,
            Action     = "CLINICAL_NOTE_CREATED",
            EntityType = "ClinicalNote",
            EntityId   = note.NoteId.ToString(),
            NewValues  = $"{{\"entities\":{entities.Count},\"sentiment\":\"{nlpResult.Sentiment}\"}}"
        });

        return ApiResponse<ClinicalNoteDto>.Ok(new ClinicalNoteDto
        {
            NoteId         = note.NoteId,
            HealthRecordId = note.HealthRecordId,
            FreeText       = note.FreeText,
            Summary        = note.Summary,
            Sentiment      = nlpResult.Sentiment,
            Entities       = nlpResult.Entities.Select(e => new EntityDto
            {
                EntityType = e.EntityType,
                Value      = e.Value,
                Confidence = e.Confidence
            }).ToList(),
            CreatedAt = note.CreatedAt
        });
    }

    public async Task<ApiResponse<List<ClinicalNoteDto>>> GetNotesByPatientAsync(
        Guid patientId, Guid doctorId)
    {
        if (!await _assignRepo.IsPatientAssignedToDoctor(patientId, doctorId))
            return ApiResponse<List<ClinicalNoteDto>>.Fail("Patient not assigned to you.");

        var notes = await _context.ClinicalNotes
            .Include(n => n.ExtractedEntities)
            .Include(n => n.HealthRecord)
            .Where(n => n.HealthRecord.PatientId == patientId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

        var dtos = notes.Select(n => new ClinicalNoteDto
        {
            NoteId         = n.NoteId,
            HealthRecordId = n.HealthRecordId,
            FreeText       = n.FreeText,
            Summary        = n.Summary,
            Entities       = n.ExtractedEntities.Select(e => new EntityDto
            {
                EntityType = e.EntityType,
                Value      = e.Value,
                Confidence = 0.85f
            }).ToList(),
            CreatedAt = n.CreatedAt
        }).ToList();

        return ApiResponse<List<ClinicalNoteDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<List<EntityDto>>> GetEntitiesAsync(
        Guid noteId, Guid doctorId)
    {
        var note = await _context.ClinicalNotes
            .Include(n => n.ExtractedEntities)
            .FirstOrDefaultAsync(n => n.NoteId == noteId && n.DoctorId == doctorId);

        if (note is null)
            return ApiResponse<List<EntityDto>>.Fail("Note not found or access denied.");

        var dtos = note.ExtractedEntities.Select(e => new EntityDto
        {
            EntityType = e.EntityType,
            Value      = e.Value,
            Confidence = 0.85f
        }).ToList();

        return ApiResponse<List<EntityDto>>.Ok(dtos);
    }
}
