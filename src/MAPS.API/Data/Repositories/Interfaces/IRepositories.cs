using System.Linq.Expressions;
using MAPS.API.Data.Entities;
using MAPS.Shared.DTOs.Common;

namespace MAPS.API.Data.Repositories.Interfaces;

// ─── Generic Repository Interface ────────────────────────────────────────────
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<PagedResult<T>> GetPagedAsync(int page, int pageSize, Expression<Func<T, bool>>? filter = null);
    Task<T> AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(Guid id);
    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate);
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null);
}

// ─── Specific Repository Interfaces ──────────────────────────────────────────
public interface IUserRepository : IRepository<AppUser>
{
    Task<AppUser?> GetByEmailAsync(string email);
    Task<IEnumerable<AppUser>> GetPendingApprovalsAsync();
    Task<IEnumerable<AppUser>> GetByRoleAsync(MAPS.Shared.Enums.UserRole role);
    Task<bool> EmailExistsAsync(string email);
}

public interface IAssignmentRepository : IRepository<Assignment>
{
    Task<IEnumerable<Assignment>> GetByDoctorIdAsync(Guid doctorId);
    Task<IEnumerable<Assignment>> GetByPatientIdAsync(Guid patientId);
    Task<Assignment?> GetActiveAssignmentAsync(Guid doctorId, Guid patientId);
    Task<bool> IsPatientAssignedToDoctor(Guid patientId, Guid doctorId);
}

public interface IPredictionRepository : IRepository<AIPrediction>
{
    Task<IEnumerable<AIPrediction>> GetByPatientIdAsync(Guid patientId);
    Task<IEnumerable<AIPrediction>> GetByDoctorIdAsync(Guid doctorId);
    Task<IEnumerable<AIPrediction>> GetSharedWithPatientAsync(Guid patientId);
}

public interface IRiskRepository : IRepository<RiskAssessment>
{
    Task<RiskAssessment?> GetLatestByPatientAsync(Guid patientId);
    Task<IEnumerable<RiskAssessment>> GetHighRiskPatientsAsync(Guid doctorId);
    Task<IEnumerable<RiskAssessment>> GetByDoctorIdAsync(Guid doctorId);
}

public interface IChatSessionRepository : IRepository<ChatSession>
{
    Task<IEnumerable<ChatSession>> GetByDoctorIdAsync(Guid doctorId);
    Task<ChatSession?> GetWithMessagesAsync(Guid sessionId);
}

public interface IAuditRepository
{
    Task LogAsync(AuditLog entry);
    Task<IEnumerable<AuditLog>> GetByUserIdAsync(Guid userId, int limit = 50);
    Task<IEnumerable<AuditLog>> GetRecentAsync(int limit = 100);
}
