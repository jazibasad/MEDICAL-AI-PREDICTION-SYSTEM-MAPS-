using MAPS.Shared.Enums;

namespace MAPS.Shared.DTOs.Risk;

public class RiskAssessmentDto
{
    public Guid AssessmentId { get; set; }
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public double RiskScore { get; set; }
    public UrgencyTier UrgencyTier { get; set; }
    public TrendDirection TrendDirection { get; set; }
    public double PreviousScore { get; set; }
    public DateTime CalculatedAt { get; set; }
    public List<string> RiskFactors { get; set; } = new();
}

public class AlertDto
{
    public Guid AlertId { get; set; }
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public UrgencyTier Tier { get; set; }
    public double RiskScore { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
