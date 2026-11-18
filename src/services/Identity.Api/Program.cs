using System.Text;
using FluentValidation;
using Identity.Api.Api.Middleware;
using Identity.Api.Application.Commands;
using Identity.Api.Application.DTOs;
using Identity.Api.Application.Validators;
using Identity.Api.Configuration;
using Identity.Api.Domain;
using Identity.Api.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ── Options pattern ───────────────────────────────────────────────────────────
// WHY: Bind configuration sections to typed classes once, inject IOptions<T> everywhere.
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// ── Database ──────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<IdentityDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Repositories (Repository pattern) ────────────────────────────────────────
builder.Services.AddScoped<IUserRepository, UserRepository>();

// ── Application services ──────────────────────────────────────────────────────
builder.Services.AddScoped<RegisterCommandHandler>();
builder.Services.AddScoped<LoginCommandHandler>();
builder.Services.AddScoped<JwtTokenService>();

// WHY: ASP.NET Core's built-in PasswordHasher uses PBKDF2 — no extra deps needed.
builder.Services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();

// ── Validators ────────────────────────────────────────────────────────────────
builder.Services.AddScoped<IValidator<RegisterRequest>, RegisterRequestValidator>();
builder.Services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();

// ── Authentication / JWT ──────────────────────────────────────────────────────
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret is required.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddAuthorization(opt =>
{
    // WHY: Named policies keep authorization declarative.
    opt.AddPolicy("Authenticated", p => p.RequireAuthenticatedUser());
    opt.AddPolicy("AdminOnly", p => p.RequireRole(Roles.Admin));
});

// ── Rate limiter for login ────────────────────────────────────────────────────
// WHY: Prevents brute-force attacks on the login endpoint.
// 5 requests per minute per IP. Built-in .NET rate limiter — no extra package.
builder.Services.AddRateLimiter(opt =>
{
    opt.AddFixedWindowLimiter("login-policy", limiter =>
    {
        limiter.PermitLimit = 5;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiter.QueueLimit = 0;
    });
    opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ── CORS ──────────────────────────────────────────────────────────────────────
var frontendOrigin = builder.Configuration["FrontendOrigin"] ?? "http://localhost:3000";
builder.Services.AddCors(opt => opt.AddDefaultPolicy(p =>
    p.WithOrigins(frontendOrigin)
     .AllowAnyMethod()
     .AllowAnyHeader()));

// ── Health checks ─────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddDbContextCheck<IdentityDbContext>("identity-db");

// ── OpenAPI ───────────────────────────────────────────────────────────────────
builder.Services.AddOpenApi();

// ── Global error handler (RFC 7807 ProblemDetails) ───────────────────────────
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddControllers();

var app = builder.Build();

// ── Middleware pipeline ───────────────────────────────────────────────────────
app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseCors();

// WHY: Apply migrations automatically in Docker / Development so the team
// doesn't need to run dotnet ef database update manually.
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Docker"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await db.Database.MigrateAsync();

    // Seed the admin user from environment variables.
    await SeedAdminAsync(scope.ServiceProvider);

    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// ── Health endpoints ──────────────────────────────────────────────────────────
app.MapHealthChecks("/health/live");   // liveness: is the process alive?
app.MapHealthChecks("/health/ready",  // readiness: can we serve traffic?
    new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready") || check.Name == "identity-db"
    });

app.MapControllers();
app.Run();

// ── Admin seeder ──────────────────────────────────────────────────────────────
static async Task SeedAdminAsync(IServiceProvider sp)
{
    var repo = sp.GetRequiredService<IUserRepository>();
    var hasher = sp.GetRequiredService<IPasswordHasher<AppUser>>();
    var config = sp.GetRequiredService<IConfiguration>();

    var email = config["AdminEmail"]
        ?? throw new InvalidOperationException("AdminEmail env var is required.");
    var password = config["AdminPassword"]
        ?? throw new InvalidOperationException("AdminPassword env var is required.");

    // Use a fresh DbContext (not the AsNoTracking repository) for the seed write.
    var db = sp.GetRequiredService<IdentityDbContext>();
    var existing = await db.Users.FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant());
    if (existing is not null) return;  // already seeded

    var admin = new AppUser
    {
        Email = email.ToLowerInvariant(),
        Role = Identity.Api.Domain.Roles.Admin
    };
    admin.PasswordHash = hasher.HashPassword(admin, password);
    db.Users.Add(admin);
    await db.SaveChangesAsync();
}

// Make Program class accessible to WebApplicationFactory in tests.
public partial class Program { }
