using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ─── Serilog ──────────────────────────────────────────────────────────────────
builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));

// ─── Controllers & API ────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title       = builder.Configuration["Swagger:Title"] ?? "MAPS API",
        Version     = "v1",
        Description = builder.Configuration["Swagger:Description"] ?? "Medical AI Prediction System"
    });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        In           = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description  = "Enter JWT token: Bearer {your token}"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ─── CORS ─────────────────────────────────────────────────────────────────────
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
                     ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("MAPSCorsPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Required for SignalR
    });
});

// ─── Health Checks ────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks();

// ─── TODO: Services registered in subsequent chunks ───────────────────────────
// Chunk 2  → builder.Services.AddDbContext<AppDbContext>(...)
// Chunk 3  → builder.Services.AddIdentity<AppUser,AppRole>(...)
//             builder.Services.AddAuthentication(JwtBearerDefaults...)
// Chunk 4  → builder.Services.AddHangfire(...)
// Chunk 8  → builder.Services.AddSignalR()
// Chunk 11 → builder.Services.AddSingleton<IWhisperService,WhisperService>()
// Chunk 12 → builder.Services.AddScoped<IChatbotOrchestrator,ChatbotOrchestrator>()

var app = builder.Build();

// ─── Middleware Pipeline ───────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "MAPS API v1"));
}

app.UseSerilogRequestLogging();
app.UseCors("MAPSCorsPolicy");
app.UseHttpsRedirection();

// TODO Chunk 3: app.UseAuthentication(); app.UseAuthorization();
// TODO Chunk 3: app.UseMiddleware<AuditLoggingMiddleware>();
// TODO Chunk 13: app.UseMiddleware<RateLimitingMiddleware>();

app.MapControllers();
app.MapHealthChecks("/api/health");

// TODO Chunk 8: app.MapHub<ChatHub>("/api/chat");

app.Run();

// Make Program accessible for integration tests (Chunk 15)
public partial class Program { }
