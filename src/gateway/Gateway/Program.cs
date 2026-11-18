var builder = WebApplication.CreateBuilder(args);

// ── Reverse Proxy (YARP) ──────────────────────────────────────────────────────
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// ── CORS (Restricted to frontend origin) ───────────────────────────────────────
var frontendOrigin = builder.Configuration["FrontendOrigin"] ?? "http://localhost:3000";
builder.Services.AddCors(opt => opt.AddDefaultPolicy(p =>
    p.WithOrigins(frontendOrigin)
     .AllowAnyMethod()
     .AllowAnyHeader()
     .WithExposedHeaders("X-Correlation-Id")));

builder.Services.AddHealthChecks();

var app = builder.Build();

// ── Correlation ID Middleware ─────────────────────────────────────────────────
// Ensures every request passing through the gateway carries an X-Correlation-Id header.
app.Use(async (ctx, next) =>
{
    const string header = "X-Correlation-Id";
    var correlationId = ctx.Request.Headers.TryGetValue(header, out var existing) && !string.IsNullOrWhiteSpace(existing)
        ? existing.ToString()
        : Guid.NewGuid().ToString();

    ctx.Items[header] = correlationId;
    ctx.Response.Headers[header] = correlationId;

    // YARP forwards request headers, so adding to Request.Headers will forward it upstream.
    if (!ctx.Request.Headers.ContainsKey(header))
    {
        ctx.Request.Headers[header] = correlationId;
    }

    await next();
});

// Security headers
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

app.UseCors();

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

app.MapReverseProxy();

app.Run();

public partial class Program { }
