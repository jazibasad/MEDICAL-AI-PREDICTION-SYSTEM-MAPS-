using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using FluentValidation;
using FluentValidation.AspNetCore;
using Serilog;
using MAPS.API.Data;
using MAPS.API.Data.Repositories;
using MAPS.API.Data.Repositories.Interfaces;
using MAPS.API.Middleware;
using MAPS.API.Services.Auth;
using MAPS.API.Validators;
using MAPS.Shared.Constants;
using MAPS.Shared.Enums;

var builder = WebApplication.CreateBuilder(args);

// ─── Serilog ──────────────────────────────────────────────────────────────────
builder.Host.UseSerilog((ctx, lc) =>
    lc.ReadFrom.Configuration(ctx.Configuration));

// ─── Database — PostgreSQL + EF Core ──────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.EnableRetryOnFailure(3)));

// ─── Repositories ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<IUserRepository,        UserRepository>();
builder.Services.AddScoped<IAssignmentRepository,  AssignmentRepository>();
builder.Services.AddScoped<IPredictionRepository,  PredictionRepository>();
builder.Services.AddScoped<IRiskRepository,        RiskRepository>();
builder.Services.AddScoped<IChatSessionRepository, ChatSessionRepository>();
builder.Services.AddScoped<IAuditRepository,       AuditRepository>();

// ─── Auth Services ────────────────────────────────────────────────────────────
builder.Services.AddScoped<ITokenService,               TokenService>();
builder.Services.AddScoped<IAuthService,                AuthService>();
builder.Services.AddSingleton<IRegistrationLockService, RegistrationLockService>();

// ─── JWT Authentication ───────────────────────────────────────────────────────
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["JwtSettings:Issuer"],
            ValidAudience            = builder.Configuration["JwtSettings:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(
                                           Encoding.UTF8.GetBytes(
                                               builder.Configuration["JwtSettings:SecretKey"]!)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        // SignalR token support (Chunk 8)
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) &&
                    ctx.HttpContext.Request.Path.StartsWithSegments("/api/chat"))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });

// ─── Authorization Policies ───────────────────────────────────────────────────
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(PolicyNames.AdminOnly, p =>
        p.RequireClaim(ClaimTypeNames.Role, UserRole.Admin.ToString()));
    options.AddPolicy(PolicyNames.DoctorOnly, p =>
        p.RequireClaim(ClaimTypeNames.Role, UserRole.Doctor.ToString()));
    options.AddPolicy(PolicyNames.PatientOnly, p =>
        p.RequireClaim(ClaimTypeNames.Role, UserRole.Patient.ToString()));
    options.AddPolicy(PolicyNames.DoctorOrAdmin, p =>
        p.RequireClaim(ClaimTypeNames.Role,
            UserRole.Doctor.ToString(), UserRole.Admin.ToString()));
    options.AddPolicy(PolicyNames.AnyAuthenticatedUser, p =>
        p.RequireAuthenticatedUser());
});

// ─── FluentValidation ─────────────────────────────────────────────────────────
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();

// ─── Controllers & Swagger ────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "MAPS API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization", Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer", BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ─── Admin Services
builder.Services.AddScoped<MAPS.API.Services.Admin.IAdminService,
                            MAPS.API.Services.Admin.AdminService>();

// ─── Analytics Service
builder.Services.AddHttpClient();
builder.Services.AddScoped<MAPS.API.Services.Analytics.IAnalyticsService,
                            MAPS.API.Services.Analytics.AnalyticsService>();

// ─── Chatbot & Literature Services
builder.Services.AddScoped<MAPS.API.Services.Chatbot.IRagContextBuilder,
                            MAPS.API.Services.Chatbot.RagContextBuilder>();
builder.Services.AddSingleton<MAPS.API.Services.Chatbot.ISafetyGuardModule,
                               MAPS.API.Services.Chatbot.SafetyGuardModule>();
builder.Services.AddScoped<MAPS.API.Services.Chatbot.IChatbotOrchestrator,
                            MAPS.API.Services.Chatbot.ChatbotOrchestrator>();
builder.Services.AddScoped<MAPS.API.Services.Literature.ILiteratureSearchService,
                            MAPS.API.Services.Literature.LiteratureSearchService>();

// ─── Risk, NLP & Voice Services
builder.Services.AddSingleton<MAPS.ML.Risk.IRiskScoringModel,
                               MAPS.ML.Risk.RiskScoringModel>();
builder.Services.AddSingleton<MAPS.ML.NLP.IClinicalNlpPipeline,
                               MAPS.ML.NLP.ClinicalNlpPipeline>();
builder.Services.AddScoped<MAPS.API.Services.Risk.IRiskAssessmentService,
                            MAPS.API.Services.Risk.RiskAssessmentService>();
builder.Services.AddScoped<MAPS.API.Services.NLP.INlpService,
                            MAPS.API.Services.NLP.NlpService>();
builder.Services.AddSingleton<MAPS.API.Services.Voice.IWhisperTranscriptionService,
                               MAPS.API.Services.Voice.WhisperTranscriptionService>();

// ─── ONNX Image Analysis Services
builder.Services.AddSingleton<MAPS.ML.ImageAnalysis.ImagePreprocessor>();
builder.Services.AddSingleton<MAPS.ML.ImageAnalysis.IPneumoniaAnalyzer,
                               MAPS.ML.ImageAnalysis.PneumoniaAnalyzer>();
builder.Services.AddSingleton<MAPS.ML.ImageAnalysis.IBrainTumourAnalyzer,
                               MAPS.ML.ImageAnalysis.BrainTumourAnalyzer>();
builder.Services.AddSingleton<MAPS.ML.ImageAnalysis.ISkinCancerAnalyzer,
                               MAPS.ML.ImageAnalysis.SkinCancerAnalyzer>();
builder.Services.AddScoped<MAPS.API.Services.ImageAnalysis.IImageAnalysisService,
                            MAPS.API.Services.ImageAnalysis.ImageAnalysisService>();

// ─── ML.NET Prediction Services
builder.Services.AddSingleton<MAPS.ML.Prediction.IDiabetesPredictor,
                               MAPS.ML.Prediction.DiabetesPredictor>();
builder.Services.AddSingleton<MAPS.ML.Prediction.IHeartDiseasePredictor,
                               MAPS.ML.Prediction.HeartDiseasePredictor>();
builder.Services.AddSingleton<MAPS.ML.Prediction.IDifferentialDiagnosisEngine,
                               MAPS.ML.Prediction.DifferentialDiagnosisEngine>();
builder.Services.AddSingleton<MAPS.API.Services.Prediction.IOllamaService,
                               MAPS.API.Services.Prediction.OllamaService>();
builder.Services.AddScoped<MAPS.API.Services.Prediction.IPredictionService,
                            MAPS.API.Services.Prediction.PredictionService>();

// ─── Patient & Scheduling Services
builder.Services.AddScoped<MAPS.API.Services.Patient.IPatientService,
                            MAPS.API.Services.Patient.PatientService>();
builder.Services.AddScoped<MAPS.API.Services.Scheduling.IAppointmentPriorityEngine,
                            MAPS.API.Services.Scheduling.AppointmentPriorityEngine>();

// ─── Doctor Services
builder.Services.AddScoped<MAPS.API.Services.Doctor.IDoctorService,
                            MAPS.API.Services.Doctor.DoctorService>();

// ─── SignalR (Real-time chat)
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 10 * 1024 * 1024; // 10MB for file sharing
});

// ─── Storage & Reports
builder.Services.AddSingleton<MAPS.API.Services.Storage.IMinioStorageService,
                               MAPS.API.Services.Storage.MinioStorageService>();
builder.Services.AddScoped<MAPS.API.Services.Reports.IReportService,
                            MAPS.API.Services.Reports.ReportService>();

// ─── Redis Cache ──────────────────────────────────────────────────────────────
builder.Services.AddStackExchangeRedisCache(options =>
    options.Configuration = builder.Configuration.GetConnectionString("Redis"));

// ─── CORS ─────────────────────────────────────────────────────────────────────
var allowedOrigins = builder.Configuration
    .GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
    options.AddPolicy("MAPSCorsPolicy", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

// ─── Health Checks ────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection")!, name: "postgres")
    .AddRedis(builder.Configuration.GetConnectionString("Redis")!, name: "redis");

var app = builder.Build();

// ─── Auto-seed on startup ─────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
    await DbSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());

// ─── Middleware Pipeline ───────────────────────────────────────────────────────
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "MAPS API v1"));
}

app.UseSerilogRequestLogging();
app.UseCors("MAPSCorsPolicy");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<AuditLoggingMiddleware>();
app.UseMiddleware<MAPS.API.Middleware.RateLimitingMiddleware>();

app.MapControllers();
app.MapHealthChecks("/api/health");
app.MapHub<MAPS.API.Hubs.ChatHub>("/api/chat");

// ─── Register Hangfire recurring jobs
MAPS.API.BackgroundJobs.RiskRecalculationJob.RegisterRecurringJob();
MAPS.API.BackgroundJobs.ModelRetrainingJob.RegisterRecurringJob();

app.Run();

public partial class Program { }
