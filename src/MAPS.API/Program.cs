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

// ─── Doctor Services
builder.Services.AddScoped<MAPS.API.Services.Doctor.IDoctorService,
                            MAPS.API.Services.Doctor.DoctorService>();

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

app.MapControllers();
app.MapHealthChecks("/api/health");

app.Run();

public partial class Program { }
