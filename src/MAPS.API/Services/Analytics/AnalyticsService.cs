using Microsoft.EntityFrameworkCore;
using MAPS.API.Data;
using MAPS.Shared.DTOs.Common;
using MAPS.Shared.Enums;

namespace MAPS.API.Services.Analytics;

public class SystemAnalyticsDto
{
    // User metrics
    public int TotalUsers          { get; set; }
    public int TotalDoctors        { get; set; }
    public int TotalPatients       { get; set; }
    public int PendingApprovals    { get; set; }
    public int ActiveAssignments   { get; set; }

    // AI metrics
    public int TotalPredictions    { get; set; }
    public int TodayPredictions    { get; set; }
    public int TotalImageAnalyses  { get; set; }
    public int TotalChatbotQueries { get; set; }

    // Risk metrics
    public int EmergencyPatients   { get; set; }
    public int UrgentPatients      { get; set; }
    public int HighRiskTotal       { get; set; }

    // Trend data (last 7 days)
    public Dictionary<string, int> PredictionsByDay    { get; set; } = new();
    public Dictionary<string, int> PredictionsByDisease{ get; set; } = new();
    public Dictionary<string, int> UserRegistrationsByDay { get; set; } = new();

    // System health
    public List<ContainerHealthDto> ContainerStatuses  { get; set; } = new();
    public DateTime GeneratedAt    { get; set; } = DateTime.UtcNow;
}

public class ContainerHealthDto
{
    public string Name    { get; set; } = string.Empty;
    public string Status  { get; set; } = string.Empty;  // healthy, unhealthy, starting
    public string Color   { get; set; } = string.Empty;
    public string Uptime  { get; set; } = string.Empty;
}

public class AuditSummaryDto
{
    public int TotalActions        { get; set; }
    public int UniqueUsers         { get; set; }
    public int FailedLogins        { get; set; }
    public int AdminActions        { get; set; }
    public List<AuditEventDto> RecentEvents { get; set; } = new();
}

public class AuditEventDto
{
    public long     LogId      { get; set; }
    public string   UserEmail  { get; set; } = string.Empty;
    public string   Action     { get; set; } = string.Empty;
    public string   EntityType { get; set; } = string.Empty;
    public string?  IpAddress  { get; set; }
    public DateTime Timestamp  { get; set; }
}

public class FeedbackAnalyticsDto
{
    public double AverageRating    { get; set; }
    public int    TotalFeedback    { get; set; }
    public int    PositiveCount    { get; set; }
    public int    NegativeCount    { get; set; }
    public int    NeutralCount     { get; set; }
    public Dictionary<string, double> RatingByDoctor    { get; set; } = new();
    public Dictionary<string, int>    SentimentTrend    { get; set; } = new();
}

public interface IAnalyticsService
{
    Task<ApiResponse<SystemAnalyticsDto>>  GetSystemAnalyticsAsync();
    Task<ApiResponse<AuditSummaryDto>>     GetAuditSummaryAsync(int limit = 50);
    Task<ApiResponse<FeedbackAnalyticsDto>>GetFeedbackAnalyticsAsync();
    Task<ApiResponse<List<ContainerHealthDto>>> GetContainerHealthAsync();
}

public class AnalyticsService : IAnalyticsService
{
    private readonly AppDbContext          _context;
    private readonly IHttpClientFactory    _httpFactory;
    private readonly IConfiguration        _config;
    private readonly ILogger<AnalyticsService> _logger;

    public AnalyticsService(
        AppDbContext           context,
        IHttpClientFactory     httpFactory,
        IConfiguration         config,
        ILogger<AnalyticsService> logger)
    {
        _context     = context;
        _httpFactory = httpFactory;
        _config      = config;
        _logger      = logger;
    }

    public async Task<ApiResponse<SystemAnalyticsDto>> GetSystemAnalyticsAsync()
    {
        var now  = DateTime.UtcNow;
        var dto  = new SystemAnalyticsDto();

        // ── User Metrics ──────────────────────────────────────────────────────
        dto.TotalUsers       = await _context.Users.CountAsync();
        dto.TotalDoctors     = await _context.Users.CountAsync(u => u.Role == UserRole.Doctor);
        dto.TotalPatients    = await _context.Users.CountAsync(u => u.Role == UserRole.Patient);
        dto.PendingApprovals = await _context.Users.CountAsync(u => !u.IsApproved && u.IsActive);
        dto.ActiveAssignments= await _context.Assignments.CountAsync(a => a.IsActive);

        // ── AI Metrics ────────────────────────────────────────────────────────
        dto.TotalPredictions   = await _context.AIPredictions.CountAsync();
        dto.TodayPredictions   = await _context.AIPredictions
            .CountAsync(p => p.CreatedAt.Date == now.Date);
        dto.TotalImageAnalyses = await _context.MedicalImages.CountAsync();
        dto.TotalChatbotQueries= await _context.ChatbotMessages
            .CountAsync(m => m.Role == "user");

        // ── Risk Metrics ──────────────────────────────────────────────────────
        var latestRisks = await _context.RiskAssessments
            .GroupBy(r => r.PatientId)
            .Select(g => g.OrderByDescending(r => r.CalculatedAt).First())
            .ToListAsync();

        dto.EmergencyPatients = latestRisks.Count(r => r.UrgencyTier == UrgencyTier.Emergency);
        dto.UrgentPatients    = latestRisks.Count(r => r.UrgencyTier == UrgencyTier.Urgent);
        dto.HighRiskTotal     = dto.EmergencyPatients + dto.UrgentPatients;

        // ── 7-Day Prediction Trend ────────────────────────────────────────────
        for (int i = 6; i >= 0; i--)
        {
            var date  = now.Date.AddDays(-i);
            var count = await _context.AIPredictions
                .CountAsync(p => p.CreatedAt.Date == date);
            dto.PredictionsByDay[date.ToString("MMM dd")] = count;
        }

        // ── Predictions by Disease ────────────────────────────────────────────
        dto.PredictionsByDisease = await _context.AIPredictions
            .GroupBy(p => p.DiseaseType)
            .Select(g => new { Disease = g.Key.ToString(), Count = g.Count() })
            .ToDictionaryAsync(x => x.Disease, x => x.Count);

        // ── 7-Day User Registrations ──────────────────────────────────────────
        for (int i = 6; i >= 0; i--)
        {
            var date  = now.Date.AddDays(-i);
            var count = await _context.Users
                .CountAsync(u => u.CreatedAt.Date == date);
            dto.UserRegistrationsByDay[date.ToString("MMM dd")] = count;
        }

        // ── Container Health ──────────────────────────────────────────────────
        dto.ContainerStatuses = await GetContainerStatusesAsync();

        return ApiResponse<SystemAnalyticsDto>.Ok(dto);
    }

    public async Task<ApiResponse<AuditSummaryDto>> GetAuditSummaryAsync(int limit = 50)
    {
        var recentLogs = await _context.AuditLogs
            .Include(a => a.User)
            .OrderByDescending(a => a.Timestamp)
            .Take(limit)
            .ToListAsync();

        var dto = new AuditSummaryDto
        {
            TotalActions  = await _context.AuditLogs.CountAsync(),
            UniqueUsers   = await _context.AuditLogs.Select(a => a.UserId).Distinct().CountAsync(),
            FailedLogins  = await _context.AuditLogs.CountAsync(a => a.Action == "LOGIN_FAILED"),
            AdminActions  = await _context.AuditLogs
                .CountAsync(a => a.Action.StartsWith("USER_") || a.Action.StartsWith("PATIENT_")),
            RecentEvents  = recentLogs.Select(a => new AuditEventDto
            {
                LogId      = a.LogId,
                UserEmail  = a.User?.Email ?? "Unknown",
                Action     = a.Action,
                EntityType = a.EntityType,
                IpAddress  = a.IpAddress,
                Timestamp  = a.Timestamp
            }).ToList()
        };

        return ApiResponse<AuditSummaryDto>.Ok(dto);
    }

    public async Task<ApiResponse<FeedbackAnalyticsDto>> GetFeedbackAnalyticsAsync()
    {
        var feedbacks = await _context.Feedbacks
            .Include(f => f.Patient).ThenInclude(p => p.User)
            .ToListAsync();

        if (!feedbacks.Any())
            return ApiResponse<FeedbackAnalyticsDto>.Ok(new FeedbackAnalyticsDto());

        var dto = new FeedbackAnalyticsDto
        {
            AverageRating = feedbacks.Average(f => f.Rating),
            TotalFeedback = feedbacks.Count,
            PositiveCount = feedbacks.Count(f => f.SentimentLabel == "Positive"),
            NegativeCount = feedbacks.Count(f => f.SentimentLabel == "Negative"),
            NeutralCount  = feedbacks.Count(f => f.SentimentLabel == "Neutral")
        };

        // Average rating per doctor
        dto.RatingByDoctor = await _context.Feedbacks
            .GroupBy(f => f.DoctorId)
            .Select(g => new
            {
                DoctorId = g.Key,
                AvgRating= g.Average(f => f.Rating)
            })
            .Join(_context.DoctorProfiles.Include(d => d.User),
                  f => f.DoctorId, d => d.DoctorId,
                  (f, d) => new { Name = d.User.FullName, f.AvgRating })
            .ToDictionaryAsync(x => x.Name, x => Math.Round(x.AvgRating, 2));

        // Sentiment trend over last 7 days
        for (int i = 6; i >= 0; i--)
        {
            var date  = DateTime.UtcNow.Date.AddDays(-i);
            var count = feedbacks.Count(f => f.SubmittedAt.Date == date);
            dto.SentimentTrend[date.ToString("MMM dd")] = count;
        }

        return ApiResponse<FeedbackAnalyticsDto>.Ok(dto);
    }

    public async Task<ApiResponse<List<ContainerHealthDto>>> GetContainerHealthAsync()
    {
        var statuses = await GetContainerStatusesAsync();
        return ApiResponse<List<ContainerHealthDto>>.Ok(statuses);
    }

    // ── Docker health check via Docker socket / health endpoints ─────────────
    private async Task<List<ContainerHealthDto>> GetContainerStatusesAsync()
    {
        var containers = new[]
        {
            ("maps-api",       "http://localhost:5000/api/health"),
            ("maps-postgres",  null as string),
            ("maps-redis",     null),
            ("maps-minio",     "http://minio:9000/minio/health/live"),
            ("maps-ollama",    "http://ollama:11434/api/tags"),
            ("maps-whisper",   "http://maps-whisper:8090/health"),
            ("maps-grafana",   null),
            ("maps-loki",      null),
            ("maps-prometheus",null),
            ("maps-nginx",     null),
            ("maps-web",       null),
        };

        var statuses = new List<ContainerHealthDto>();

        foreach (var (name, healthUrl) in containers)
        {
            string status;
            string color;

            if (healthUrl is not null)
            {
                try
                {
                    var client   = _httpFactory.CreateClient();
                    client.Timeout = TimeSpan.FromSeconds(3);
                    var response = await client.GetAsync(healthUrl);
                    status = response.IsSuccessStatusCode ? "healthy" : "unhealthy";
                    color  = response.IsSuccessStatusCode ? "#10b981" : "#ef4444";
                }
                catch
                {
                    status = "unreachable";
                    color  = "#f59e0b";
                }
            }
            else
            {
                // No health endpoint — mark as assumed running
                status = "running";
                color  = "#6b7280";
            }

            statuses.Add(new ContainerHealthDto
            {
                Name   = name,
                Status = status,
                Color  = color,
                Uptime = status == "healthy" || status == "running" ? "online" : "offline"
            });
        }

        return statuses;
    }
}
