using Microsoft.EntityFrameworkCore;
using MAPS.API.Data.Entities;
using MAPS.API.Data.Repositories.Interfaces;
using MAPS.Shared.Enums;

namespace MAPS.API.Data.Repositories;

// ─── User Repository ──────────────────────────────────────────────────────────
public class UserRepository : BaseRepository<AppUser>, IUserRepository
{
    public UserRepository(AppDbContext context) : base(context) { }

    public async Task<AppUser?> GetByEmailAsync(string email) =>
        await _dbSet
            .Include(u => u.DoctorProfile)
            .Include(u => u.PatientProfile)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());

    public async Task<IEnumerable<AppUser>> GetPendingApprovalsAsync() =>
        await _dbSet
            .Where(u => !u.IsApproved && u.IsActive)
            .OrderBy(u => u.CreatedAt)
            .ToListAsync();

    public async Task<IEnumerable<AppUser>> GetByRoleAsync(UserRole role) =>
        await _dbSet
            .Where(u => u.Role == role && u.IsActive)
            .Include(u => u.DoctorProfile)
            .Include(u => u.PatientProfile)
            .ToListAsync();

    public async Task<bool> EmailExistsAsync(string email) =>
        await _dbSet.AnyAsync(u => u.Email.ToLower() == email.ToLower());
}

// ─── Assignment Repository ────────────────────────────────────────────────────
public class AssignmentRepository : BaseRepository<Assignment>, IAssignmentRepository
{
    public AssignmentRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<Assignment>> GetByDoctorIdAsync(Guid doctorId) =>
        await _context.Assignments
            .Where(a => a.DoctorId == doctorId && a.IsActive)
            .Include(a => a.Patient).ThenInclude(p => p.User)
            .Include(a => a.Patient).ThenInclude(p => p.RiskAssessments)
            .OrderBy(a => a.AssignedDate)
            .ToListAsync();

    public async Task<IEnumerable<Assignment>> GetByPatientIdAsync(Guid patientId) =>
        await _context.Assignments
            .Where(a => a.PatientId == patientId)
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .ToListAsync();

    public async Task<Assignment?> GetActiveAssignmentAsync(Guid doctorId, Guid patientId) =>
        await _context.Assignments
            .FirstOrDefaultAsync(a =>
                a.DoctorId  == doctorId &&
                a.PatientId == patientId &&
                a.IsActive);

    public async Task<bool> IsPatientAssignedToDoctor(Guid patientId, Guid doctorId) =>
        await _context.Assignments
            .AnyAsync(a =>
                a.PatientId == patientId &&
                a.DoctorId  == doctorId &&
                a.IsActive);
}

// ─── Prediction Repository ────────────────────────────────────────────────────
public class PredictionRepository : BaseRepository<AIPrediction>, IPredictionRepository
{
    public PredictionRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<AIPrediction>> GetByPatientIdAsync(Guid patientId) =>
        await _context.AIPredictions
            .Where(p => p.PatientId == patientId)
            .Include(p => p.DifferentialDiagnoses)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

    public async Task<IEnumerable<AIPrediction>> GetByDoctorIdAsync(Guid doctorId) =>
        await _context.AIPredictions
            .Where(p => p.DoctorId == doctorId)
            .Include(p => p.Patient).ThenInclude(pt => pt.User)
            .Include(p => p.DifferentialDiagnoses)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

    public async Task<IEnumerable<AIPrediction>> GetSharedWithPatientAsync(Guid patientId) =>
        await _context.AIPredictions
            .Where(p => p.PatientId == patientId && p.IsSharedWithPatient)
            .Include(p => p.DifferentialDiagnoses)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
}

// ─── Risk Repository ──────────────────────────────────────────────────────────
public class RiskRepository : BaseRepository<RiskAssessment>, IRiskRepository
{
    public RiskRepository(AppDbContext context) : base(context) { }

    public async Task<RiskAssessment?> GetLatestByPatientAsync(Guid patientId) =>
        await _context.RiskAssessments
            .Where(r => r.PatientId == patientId)
            .OrderByDescending(r => r.CalculatedAt)
            .FirstOrDefaultAsync();

    public async Task<IEnumerable<RiskAssessment>> GetHighRiskPatientsAsync(Guid doctorId) =>
        await _context.RiskAssessments
            .Where(r =>
                r.DoctorId   == doctorId &&
                r.UrgencyTier <= UrgencyTier.Urgent)
            .Include(r => r.Patient).ThenInclude(p => p.User)
            .OrderByDescending(r => r.RiskScore)
            .ToListAsync();

    public async Task<IEnumerable<RiskAssessment>> GetByDoctorIdAsync(Guid doctorId) =>
        await _context.RiskAssessments
            .Where(r => r.DoctorId == doctorId)
            .Include(r => r.Patient).ThenInclude(p => p.User)
            .OrderByDescending(r => r.CalculatedAt)
            .ToListAsync();
}

// ─── ChatSession Repository ───────────────────────────────────────────────────
public class ChatSessionRepository : BaseRepository<ChatSession>, IChatSessionRepository
{
    public ChatSessionRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<ChatSession>> GetByDoctorIdAsync(Guid doctorId) =>
        await _context.ChatSessions
            .Where(s => s.DoctorId == doctorId)
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync();

    public async Task<ChatSession?> GetWithMessagesAsync(Guid sessionId) =>
        await _context.ChatSessions
            .Include(s => s.Messages)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);
}

// ─── AuditLog Repository — Append Only ───────────────────────────────────────
public class AuditRepository : IAuditRepository
{
    private readonly AppDbContext _context;

    public AuditRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task LogAsync(AuditLog entry)
    {
        await _context.AuditLogs.AddAsync(entry);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<AuditLog>> GetByUserIdAsync(Guid userId, int limit = 50) =>
        await _context.AuditLogs
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.Timestamp)
            .Take(limit)
            .ToListAsync();

    public async Task<IEnumerable<AuditLog>> GetRecentAsync(int limit = 100) =>
        await _context.AuditLogs
            .Include(a => a.User)
            .OrderByDescending(a => a.Timestamp)
            .Take(limit)
            .ToListAsync();
}
