using MAPS.Shared.Enums;

namespace MAPS.Shared.DTOs.User;

public class UserDto
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; }
    public bool IsApproved { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class DoctorProfileDto : UserDto
{
    public Guid DoctorId { get; set; }
    public string Specialization { get; set; } = string.Empty;
    public string LicenseNumber { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public int AssignedPatientCount { get; set; }
}

public class PatientProfileDto : UserDto
{
    public Guid PatientId { get; set; }
    public string BloodGroup { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public string EmergencyContact { get; set; } = string.Empty;
    public Guid? AssignedDoctorId { get; set; }
    public string? AssignedDoctorName { get; set; }
    public double CurrentRiskScore { get; set; }
    public UrgencyTier UrgencyTier { get; set; }
}

public class UpdateUserRequest
{
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public bool? IsActive { get; set; }
}

public class UpdateDoctorProfileRequest
{
    public string? Specialization { get; set; }
    public string? Department { get; set; }
    public string? LicenseNumber { get; set; }
}

public class UpdatePatientProfileRequest
{
    public string? BloodGroup { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? EmergencyContact { get; set; }
}

public class UserSummaryDto
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; }
}
