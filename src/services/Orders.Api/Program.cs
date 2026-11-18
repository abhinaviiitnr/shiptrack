using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Orders.Api.Api.Middleware;
using Orders.Api.Application.Commands;
using Orders.Api.Application.DTOs;
using Orders.Api.Application.Queries;
using Orders.Api.Application.Validators;
using Orders.Api.Configuration;
using Orders.Api.Domain;
using Orders.Api.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ── Options pattern ───────────────────────────────────────────────────────────
builder.Services.AddOptions<InventoryServiceOptions>()
    .Bind(builder.Configuration.GetSection(InventoryServiceOptions.SectionName))
    .ValidateOnStart();

// ── Database ──────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<OrdersDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Repositories & Clients ────────────────────────────────────────────────────
builder.Services.AddScoped<IOrderRepository, OrderRepository>();

// Resilient Typed HttpClient for Inventory
builder.Services.AddHttpClient<IInventoryClient, InventoryClient>((sp, client) =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<InventoryServiceOptions>>().Value;
    client.BaseAddress = new Uri(opts.BaseUrl);
})
.AddStandardResilienceHandler(opt =>
{
    // 3s total request timeout
    opt.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(3);
    opt.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
    // 2 retries with backoff
    opt.Retry.MaxRetryAttempts = 2;
    opt.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
    // Circuit breaker
    opt.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(10);
    opt.CircuitBreaker.FailureRatio = 0.5;
});

// ── Shipping Strategy ─────────────────────────────────────────────────────────
builder.Services.AddSingleton<IShippingStrategy, StandardShipping>();
builder.Services.AddSingleton<IShippingStrategy, ExpressShipping>();
builder.Services.AddSingleton<ShippingStrategySelector>();

// ── CQRS Handlers ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<CreateOrderHandler>();
builder.Services.AddScoped<UpdateOrderStatusHandler>();
builder.Services.AddScoped<CancelOrderHandler>();
builder.Services.AddScoped<GetOrderByIdQuery>();
builder.Services.AddScoped<GetCustomerOrdersQuery>();
builder.Services.AddScoped<GetAdminOrdersQuery>();

// ── Validators ────────────────────────────────────────────────────────────────
builder.Services.AddScoped<IValidator<CreateOrderRequest>, CreateOrderRequestValidator>();

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
    opt.AddPolicy("Authenticated", p => p.RequireAuthenticatedUser());
    opt.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
});

// ── CORS ──────────────────────────────────────────────────────────────────────
var frontendOrigin = builder.Configuration["FrontendOrigin"] ?? "http://localhost:3000";
builder.Services.AddCors(opt => opt.AddDefaultPolicy(p =>
    p.WithOrigins(frontendOrigin).AllowAnyMethod().AllowAnyHeader()));

// ── Health checks ─────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddDbContextCheck<OrdersDbContext>("orders-db");

builder.Services.AddOpenApi();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddControllers();

var app = builder.Build();

app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseCors();

// Check for --seed-large CLI argument
if (args.Contains("--seed-large"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await db.Database.MigrateAsync();
    await LargeOrderSeeder.SeedAsync(db, logger);
    return;
}

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Docker"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
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
        Predicate = check => check.Name == "orders-db"
    });

app.MapControllers();
app.Run();

public partial class Program { }
