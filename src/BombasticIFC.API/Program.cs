using System.Text;
using BombasticIFC.Application.Common.Interfaces;
using BombasticIFC.Domain.Repositories;
using BombasticIFC.Infrastructure.Persistence;
using BombasticIFC.Infrastructure.Repositories;
using BombasticIFC.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add MediatR
builder.Services.AddMediatR(cfg => 
    cfg.RegisterServicesFromAssembly(typeof(BombasticIFC.Application.UseCases.Models.UploadModelCommand).Assembly));

// Add Database
// Railway injects DATABASE_URL (postgres://user:pass@host:port/db).
// Npgsql accepts this URL format directly, so prefer it over the
// key=value connection string — avoids special-character escaping issues.
var connectionString =
    Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        connectionString,
        b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

builder.Services.AddScoped<IApplicationDbContext>(provider => 
    provider.GetRequiredService<ApplicationDbContext>());

// Add Repositories
builder.Services.AddScoped<IIfcModelRepository, IfcModelRepository>();
builder.Services.AddScoped<IConversionJobRepository, ConversionJobRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();

// Add Services
var storagePath = builder.Configuration.GetValue<string>("StoragePath") ?? "/data/storage";
builder.Services.AddSingleton<IFileStorageService>(new FileStorageService(storagePath));
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IPasswordHasherService, PasswordHasherService>();
builder.Services.AddScoped<IIfcConversionService>(provider =>
    new IfcConversionService(
        storagePath,
        provider.GetRequiredService<ILogger<IfcConversionService>>()));
builder.Services.AddHostedService<ConversionWorker>();

// Add JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["Secret"] 
    ?? throw new InvalidOperationException("JWT Secret is not configured");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"] ?? "BombasticIFC",
        ValidAudience = jwtSettings["Audience"] ?? "BombasticIFC-Client",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
});

builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    // Auth endpoints: 10 requests per minute per IP (login, register, refresh)
    options.AddFixedWindowLimiter("auth", limiterOptions =>
    {
        limiterOptions.PermitLimit = 10;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 0;
    });

    // Upload endpoint: 5 uploads per minute per IP (IFC file processing is expensive)
    options.AddFixedWindowLimiter("upload", limiterOptions =>
    {
        limiterOptions.PermitLimit = 5;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 0;
    });

    // General API endpoints: 20 requests per minute per IP (conversion jobs etc.)
    options.AddFixedWindowLimiter("api", limiterOptions =>
    {
        limiterOptions.PermitLimit = 20;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 0;
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        var logger = context.HttpContext.RequestServices
            .GetRequiredService<ILogger<Program>>();
        logger.LogWarning(
            "Rate limit exceeded: {Method} {Path} from {RemoteIP}",
            context.HttpContext.Request.Method,
            context.HttpContext.Request.Path,
            context.HttpContext.Connection.RemoteIpAddress);

        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsync(
            "{\"message\":\"Too many requests. Please slow down.\"}",
            cancellationToken);
    };
});

// Add CORS
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        if (builder.Environment.IsDevelopment() || allowedOrigins.Length == 0)
        {
            policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
        }
        else
        {
            policy.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader();
        }
    });
});

var app = builder.Build();

// ── HTTP pipeline ────────────────────────────────────────────────────────────
// Register all middleware and routes BEFORE touching the database.
// This guarantees /health is always reachable even when the DB is misconfigured,
// so Railway's healthcheck can pass and the real error shows up in the logs.

// Always expose Swagger (dev + prod — accessible via NodePort / ingress)
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowAll");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Health endpoint — intentionally DB-agnostic.
// Returning 200 here means "the process is alive and listening".
// A failing DB will surface in API call errors, not in this probe.
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// ── Database startup ─────────────────────────────────────────────────────────
// Single migration + seed block with full error containment.
// If the DB is unreachable (wrong connection string, Postgres not ready) the
// exception is logged and swallowed so app.Run() is always reached.
// Missing ConnectionStrings__DefaultConnection is the most common Railway cause.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        db.Database.Migrate();
        logger.LogInformation("Database migrations applied successfully");

        await DatabaseSeeder.SeedAsync(
            db,
            builder.Configuration.GetValue<string>("StoragePath") ?? "/data/storage",
            logger);
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex,
            "Database startup failed. The app will run in degraded mode — " +
            "all DB-backed endpoints will return errors until the connection is fixed. " +
            "Check ConnectionStrings__DefaultConnection (Railway: use PGHOST/PGPORT/PGUSER/PGPASSWORD variables).");
    }
}

app.Run();
