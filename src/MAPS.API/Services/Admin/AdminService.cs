using Microsoft.EntityFrameworkCore;
using MAPS.API.Data;
using MAPS.API.Data.Entities;
using MAPS.API.Data.Repositories.Interfaces;
using MAPS.Shared.DTOs.Common;
using MAPS.Shared.DTOs.User;
using MAPS.Shared.Enums;

namespace MAPS.API.Services.Admin;

public interface IAdminService
{
    // User Management
    Task<ApiResponse<PagedResult<UserDto>>>    GetAllUsersAsync(PaginationRequest req);
    Task<ApiResponse<UserDto>>                 GetUserByIdAsync(Guid userId);
    Task<ApiResponse<PagedResult<UserDto>>>    GetPendingApprovalsAsync();
    Task<ApiResponse>                          ApproveUserAsync(Guid userId, Guid adminId);
    Task<ApiResponse>                          DeactivateUserAsync(Guid userId, Guid adminId);
    Task<ApiResponse>                          ActivateUserAsync(Guid userId, Guid adminId);
    Task<ApiResponse>                          DeleteUserAsync(Guid userId, Guid adminId);

    // Doctor-Patient Assignment
    Task<ApiResponse>                          AssignPatientToDoctorAsync(Guid doctorId, Guid patientId, Guid adminId);
    Task<ApiResponse>                          UnassignPatientAsync(Guid assignmentId, Guid adminId);
    Task<ApiResponse>                          TransferPatientAsync(Guid patientId, Guid newDoctorId, Guid adminId);
    Task<ApiResponse<List<DoctorProfileDto>>>  GetAllDoctorsAsync();
    Task<ApiResponse<List<PatientProfileDto>>> GetUnassignedPatientsAsync();

    // System Analytics
    Task<ApiResponse<AdminDashboardStats>>     GetDashboardStatsAsync();
}

public class AdminDashboardStats
{
    public int TotalUsers       { get; set; }
    public int TotalDoctors     { get; set; }
    public int TotalPatients    { get; set; }
    public int PendingApprovals { get; set; }
    public int TotalPredictions { get; set; }
    public int TodayPredictions { get; set; }
    public int ActiveAssignments { get; set; }
    public int HighRiskPatients { get; set; }
    public Dictionary<string, int> PredictionsByDisease { get; set; } = new();
    public Dictionary<string, int> PredictionsByDay     { get; set; } = new();
}

public class AdminService : IAdminService
{
    private readonly AppDbContext        _context;
    private readonly IUserRepository     _userRepo;
    private readonly IAssignmentRepository _assignRepo;
    private readonly IAuditRepository    _auditRepo;
    private readonly ILogger<AdminService> _logger;

    public AdminService(
        AppDbContext          context,
        IUserRepository       userRepo,
        IAssignmentRepository assignRepo,
        IAuditRepository      auditRepo,
        ILogger<AdminService> logger)
    {
        _context    = context;
        _userRepo   = userRepo;
        _assignRepo = assignRepo;
        _auditRepo  = auditRepo;
        _logger     = logger;
    }

    // ── Get All Users ─────────────────────────────────────────────────────────
    public async Task<ApiResponse<PagedResult<UserDto>>> GetAllUsersAsync(PaginationRequest req)
    {
        var query = _context.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(req.SearchTerm))
            query = query.Where(u =>
                u.FullName.Contains(req.SearchTerm) ||
                u.Email.Contains(req.SearchTerm));

        query = req.SortBy?.ToLower() switch
        {
            "email"     => req.SortDescending ? query.OrderByDescending(u => u.Email)     : query.OrderBy(u => u.Email),
            "role"      => req.SortDescending ? query.OrderByDescending(u => u.Role)      : query.OrderBy(u => u.Role),
            "createdat" => req.SortDescending ? query.OrderByDescending(u => u.CreatedAt) : query.OrderBy(u => u.CreatedAt),
            _           => query.OrderBy(u => u.FullName)
        };

        var total = await query.CountAsync();
        var users = await query
            .Skip((req.Page - 1) * req.PageSize)
            .Take(req.PageSize)
            .Select(u => new UserDto
            {
                UserId     = u.UserId,
                FullName   = u.FullName,
                Email      = u.Email,
                Role       = u.Role,
                IsActive   = u.IsActive,
                IsApproved = u.IsApproved,
                CreatedAt  = u.CreatedAt
            })
            .ToListAsync();

        return ApiResponse<PagedResult<UserDto>>.Ok(new PagedResult<UserDto>
        {
            Items      = users,
            TotalCount = total,
            Page       = req.Page,
            PageSize   = req.PageSize
        });
    }

    // ── Get User By Id ────────────────────────────────────────────────────────
    public async Task<ApiResponse<UserDto>> GetUserByIdAsync(Guid userId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user is null)
            return ApiResponse<UserDto>.Fail("User not found.");

        return ApiResponse<UserDto>.Ok(MapToDto(user));
    }

    // ── Pending Approvals ─────────────────────────────────────────────────────
    public async Task<ApiResponse<PagedResult<UserDto>>> GetPendingApprovalsAsync()
    {
        var users = await _userRepo.GetPendingApprovalsAsync();
        var dtos  = users.Select(MapToDto).ToList();
        return ApiResponse<PagedResult<UserDto>>.Ok(new PagedResult<UserDto>
        {
            Items      = dtos,
            TotalCount = dtos.Count,
            Page       = 1,
            PageSize   = dtos.Count
        });
    }

    // ── Approve User ──────────────────────────────────────────────────────────
    public async Task<ApiResponse> ApproveUserAsync(Guid userId, Guid adminId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user is null) return ApiResponse.Fail("User not found.");
        if (user.IsApproved) return ApiResponse.Fail("User is already approved.");

        user.IsApproved = true;
        await _userRepo.UpdateAsync(user);

        await _auditRepo.LogAsync(new AuditLog
        {
            UserId     = adminId,
            Action     = "USER_APPROVED",
            EntityType = "User",
            EntityId   = userId.ToString(),
            NewValues  = $"{{\"approved\":true}}"
        });

        _logger.LogInformation("Admin {AdminId} approved user {UserId}", adminId, userId);
        return ApiResponse.Ok($"User '{user.FullName}' approved successfully.");
    }

    // ── Deactivate User ───────────────────────────────────────────────────────
    public async Task<ApiResponse> DeactivateUserAsync(Guid userId, Guid adminId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user is null) return ApiResponse.Fail("User not found.");
        if (user.Role == UserRole.Admin) return ApiResponse.Fail("Cannot deactivate an admin account.");

        user.IsActive = false;
        await _userRepo.UpdateAsync(user);

        await _auditRepo.LogAsync(new AuditLog
        {
            UserId     = adminId,
            Action     = "USER_DEACTIVATED",
            EntityType = "User",
            EntityId   = userId.ToString()
        });

        return ApiResponse.Ok($"User '{user.FullName}' deactivated.");
    }

    // ── Activate User ─────────────────────────────────────────────────────────
    public async Task<ApiResponse> ActivateUserAsync(Guid userId, Guid adminId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user is null) return ApiResponse.Fail("User not found.");

        user.IsActive = true;
        await _userRepo.UpdateAsync(user);

        await _auditRepo.LogAsync(new AuditLog
        {
            UserId     = adminId,
            Action     = "USER_ACTIVATED",
            EntityType = "User",
            EntityId   = userId.ToString()
        });

        return ApiResponse.Ok($"User '{user.FullName}' activated.");
    }

    // ── Delete User ───────────────────────────────────────────────────────────
    public async Task<ApiResponse> DeleteUserAsync(Guid userId, Guid adminId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user is null) return ApiResponse.Fail("User not found.");
        if (user.Role == UserRole.Admin) return ApiResponse.Fail("Cannot delete an admin account.");

        // Soft delete — deactivate instead of hard delete to preserve audit trail
        user.IsActive   = false;
        user.IsApproved = false;
        await _userRepo.UpdateAsync(user);

        await _auditRepo.LogAsync(new AuditLog
        {
            UserId     = adminId,
            Action     = "USER_DELETED",
            EntityType = "User",
            EntityId   = userId.ToString()
        });

        return ApiResponse.Ok($"User '{user.FullName}' deleted.");
    }

    // ── Assign Patient to Doctor ──────────────────────────────────────────────
    public async Task<ApiResponse> AssignPatientToDoctorAsync(
        Guid doctorId, Guid patientId, Guid adminId)
    {
        var existing = await _assignRepo.GetActiveAssignmentAsync(doctorId, patientId);
        if (existing is not null)
            return ApiResponse.Fail("Patient is already assigned to this doctor.");

        var doctor  = await _context.DoctorProfiles.FindAsync(doctorId);
        var patient = await _context.PatientProfiles.FindAsync(patientId);
        if (doctor  is null) return ApiResponse.Fail("Doctor not found.");
        if (patient is null) return ApiResponse.Fail("Patient not found.");

        var assignment = new Assignment
        {
            DoctorId  = doctorId,
            PatientId = patientId,
            IsActive  = true
        };

        _context.Assignments.Add(assignment);
        await _context.SaveChangesAsync();

        await _auditRepo.LogAsync(new AuditLog
        {
            UserId     = adminId,
            Action     = "PATIENT_ASSIGNED",
            EntityType = "Assignment",
            EntityId   = assignment.AssignmentId.ToString(),
            NewValues  = $"{{\"doctorId\":\"{doctorId}\",\"patientId\":\"{patientId}\"}}"
        });

        return ApiResponse.Ok("Patient assigned to doctor successfully.");
    }

    // ── Unassign Patient ──────────────────────────────────────────────────────
    public async Task<ApiResponse> UnassignPatientAsync(Guid assignmentId, Guid adminId)
    {
        var assignment = await _context.Assignments.FindAsync(assignmentId);
        if (assignment is null) return ApiResponse.Fail("Assignment not found.");

        assignment.IsActive = false;
        await _context.SaveChangesAsync();

        await _auditRepo.LogAsync(new AuditLog
        {
            UserId     = adminId,
            Action     = "PATIENT_UNASSIGNED",
            EntityType = "Assignment",
            EntityId   = assignmentId.ToString()
        });

        return ApiResponse.Ok("Assignment removed successfully.");
    }

    // ── Transfer Patient ──────────────────────────────────────────────────────
    public async Task<ApiResponse> TransferPatientAsync(
        Guid patientId, Guid newDoctorId, Guid adminId)
    {
        // Deactivate all current active assignments
        var currentAssignments = await _context.Assignments
            .Where(a => a.PatientId == patientId && a.IsActive)
            .ToListAsync();

        foreach (var a in currentAssignments)
            a.IsActive = false;

        // Create new assignment
        _context.Assignments.Add(new Assignment
        {
            DoctorId  = newDoctorId,
            PatientId = patientId,
            IsActive  = true
        });

        await _context.SaveChangesAsync();

        await _auditRepo.LogAsync(new AuditLog
        {
            UserId     = adminId,
            Action     = "PATIENT_TRANSFERRED",
            EntityType = "Assignment",
            NewValues  = $"{{\"newDoctorId\":\"{newDoctorId}\",\"patientId\":\"{patientId}\"}}"
        });

        return ApiResponse.Ok("Patient transferred to new doctor successfully.");
    }

    // ── Get All Doctors ───────────────────────────────────────────────────────
    public async Task<ApiResponse<List<DoctorProfileDto>>> GetAllDoctorsAsync()
    {
        var doctors = await _context.DoctorProfiles
            .Include(d => d.User)
            .Include(d => d.Assignments.Where(a => a.IsActive))
            .Where(d => d.User.IsActive && d.User.IsApproved)
            .Select(d => new DoctorProfileDto
            {
                DoctorId             = d.DoctorId,
                UserId               = d.UserId,
                FullName             = d.User.FullName,
                Email                = d.User.Email,
                Role                 = UserRole.Doctor,
                IsActive             = d.User.IsActive,
                IsApproved           = d.User.IsApproved,
                CreatedAt            = d.User.CreatedAt,
                Specialization       = d.Specialization,
                LicenseNumber        = d.LicenseNumber,
                Department           = d.Department,
                AssignedPatientCount = d.Assignments.Count(a => a.IsActive)
            })
            .ToListAsync();

        return ApiResponse<List<DoctorProfileDto>>.Ok(doctors);
    }

    // ── Get Unassigned Patients ───────────────────────────────────────────────
    public async Task<ApiResponse<List<PatientProfileDto>>> GetUnassignedPatientsAsync()
    {
        var assignedPatientIds = await _context.Assignments
            .Where(a => a.IsActive)
            .Select(a => a.PatientId)
            .Distinct()
            .ToListAsync();

        var patients = await _context.PatientProfiles
            .Include(p => p.User)
            .Where(p => p.User.IsActive &&
                        p.User.IsApproved &&
                        !assignedPatientIds.Contains(p.PatientId))
            .Select(p => new PatientProfileDto
            {
                PatientId  = p.PatientId,
                UserId     = p.UserId,
                FullName   = p.User.FullName,
                Email      = p.User.Email,
                Role       = UserRole.Patient,
                IsActive   = p.User.IsActive,
                IsApproved = p.User.IsApproved,
                CreatedAt  = p.User.CreatedAt,
                BloodGroup = p.BloodGroup
            })
            .ToListAsync();

        return ApiResponse<List<PatientProfileDto>>.Ok(patients);
    }

    // ── Dashboard Stats ───────────────────────────────────────────────────────
    public async Task<ApiResponse<AdminDashboardStats>> GetDashboardStatsAsync()
    {
        var stats = new AdminDashboardStats
        {
            TotalUsers        = await _context.Users.CountAsync(),
            TotalDoctors      = await _context.Users.CountAsync(u => u.Role == UserRole.Doctor),
            TotalPatients     = await _context.Users.CountAsync(u => u.Role == UserRole.Patient),
            PendingApprovals  = await _context.Users.CountAsync(u => !u.IsApproved && u.IsActive),
            TotalPredictions  = await _context.AIPredictions.CountAsync(),
            TodayPredictions  = await _context.AIPredictions.CountAsync(p =>
                                    p.CreatedAt.Date == DateTime.UtcNow.Date),
            ActiveAssignments = await _context.Assignments.CountAsync(a => a.IsActive),
            HighRiskPatients  = await _context.RiskAssessments
                                    .GroupBy(r => r.PatientId)
                                    .CountAsync(g => g.OrderByDescending(r => r.CalculatedAt)
                                                      .First().UrgencyTier <= UrgencyTier.Urgent)
        };

        stats.PredictionsByDisease = await _context.AIPredictions
            .GroupBy(p => p.DiseaseType)
            .Select(g => new { Disease = g.Key.ToString(), Count = g.Count() })
            .ToDictionaryAsync(x => x.Disease, x => x.Count);

        // Last 7 days prediction trend
        for (int i = 6; i >= 0; i--)
        {
            var date  = DateTime.UtcNow.Date.AddDays(-i);
            var count = await _context.AIPredictions
                .CountAsync(p => p.CreatedAt.Date == date);
            stats.PredictionsByDay[date.ToString("MMM dd")] = count;
        }

        return ApiResponse<AdminDashboardStats>.Ok(stats);
    }

    private static UserDto MapToDto(AppUser u) => new()
    {
        UserId     = u.UserId,
        FullName   = u.FullName,
        Email      = u.Email,
        Role       = u.Role,
        IsActive   = u.IsActive,
        IsApproved = u.IsApproved,
        CreatedAt  = u.CreatedAt
    };
}
