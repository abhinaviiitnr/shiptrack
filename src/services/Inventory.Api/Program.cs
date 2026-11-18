using System.Text;
using FluentValidation;
using Inventory.Api.Api.Middleware;
using Inventory.Api.Application.Commands;
using Inventory.Api.Application.DTOs;
using Inventory.Api.Application.Queries;
using Inventory.Api.Application.Validators;
using Inventory.Api.Configuration;
using Inventory.Api.Domain;
using Inventory.Api.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ── Options pattern ───────────────────────────────────────────────────────────
builder.Services.AddOptions<InternalKeyOptions>()
    .Bind(builder.Configuration.GetSection(InternalKeyOptions.SectionName))
    .ValidateOnStart();

// ── Database ──────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<InventoryDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Repositories ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<IProductRepository, ProductRepository>();

// ── CQRS handlers ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<GetProductsQuery>();
builder.Services.AddScoped<GetProductByIdQuery>();
builder.Services.AddScoped<GetProductBatchQuery>();
builder.Services.AddScoped<CreateProductHandler>();
builder.Services.AddScoped<UpdateProductHandler>();
builder.Services.AddScoped<DeleteProductHandler>();
builder.Services.AddScoped<ReserveStockHandler>();
builder.Services.AddScoped<ReleaseStockHandler>();

// ── Validators ────────────────────────────────────────────────────────────────
builder.Services.AddScoped<IValidator<CreateProductRequest>, CreateProductRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateProductRequest>, UpdateProductRequestValidator>();

// ── JWT (validate tokens issued by Identity service) ─────────────────────────
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
    opt.AddPolicy("Authenticated", p => p.RequireAuthenticatedUser());
    opt.AddPolicy("AdminOnly", p => p.RequireRole(Roles.Admin));
});

// ── CORS ──────────────────────────────────────────────────────────────────────
var frontendOrigin = builder.Configuration["FrontendOrigin"] ?? "http://localhost:3000";
builder.Services.AddCors(opt => opt.AddDefaultPolicy(p =>
    p.WithOrigins(frontendOrigin).AllowAnyMethod().AllowAnyHeader()));

// ── Health checks ─────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddDbContextCheck<InventoryDbContext>("inventory-db");

builder.Services.AddOpenApi();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddControllers();

var app = builder.Build();

app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseCors();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Docker"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    await db.Database.MigrateAsync();

    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready",
    new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Name == "inventory-db"
    });

app.MapControllers();
app.Run();

public partial class Program { }
