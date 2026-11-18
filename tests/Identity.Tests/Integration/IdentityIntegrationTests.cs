using System.Net;
using System.Net.Http.Json;
using Identity.Api.Application.DTOs;
using Identity.Api.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Identity.Tests.Integration;

public class IdentityIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("identity_test_db")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((ctx, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Secret"] = "test-jwt-secret-at-least-32-chars-long!!",
                    ["Jwt:Issuer"] = "ShipTrack",
                    ["Jwt:Audience"] = "ShipTrackClient",
                    ["AdminEmail"] = "admin@shiptrack.dev",
                    ["AdminPassword"] = "Admin@123456"
                });
            });
            builder.ConfigureTestServices(services =>
            {
                // Remove existing DbContext registration
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<IdentityDbContext>));
                if (descriptor is not null) services.Remove(descriptor);

                // Add test DB connection
                services.AddDbContext<IdentityDbContext>(options =>
                    options.UseNpgsql(_dbContainer.GetConnectionString()));
            });
        });

        // Ensure database schema is migrated
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.MigrateAsync();

        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _dbContainer.DisposeAsync();
    }

    [Fact]
    public async Task Register_And_Login_HappyPath_ReturnsToken()
    {
        var email = $"cust_{Guid.NewGuid()}@shiptrack.dev";
        var password = "SecurePassword123";

        // 1. Register
        var regResponse = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, password));
        Assert.Equal(HttpStatusCode.Created, regResponse.StatusCode);

        var regResult = await regResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(regResult);
        Assert.NotNull(regResult.Token);
        Assert.Equal("Customer", regResult.Role);

        // 2. Duplicate Registration fails with 409
        var dupResponse = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, password));
        Assert.Equal(HttpStatusCode.Conflict, dupResponse.StatusCode);

        // 3. Login with correct credentials
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(loginResult);
        Assert.NotNull(loginResult.Token);

        // 4. Login with invalid password returns 401
        var badLogin = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "WrongPassword999"));
        Assert.Equal(HttpStatusCode.Unauthorized, badLogin.StatusCode);
    }

    [Fact]
    public async Task Login_RateLimiter_Returns429_WhenLimitExceeded()
    {
        // Rate limiter allows 5 per minute per IP. The 6th should return 429 TooManyRequests.
        var client = _factory.CreateClient();
        var email = "rate_limit_test@shiptrack.dev";

        HttpResponseMessage lastResponse = null!;
        for (var i = 0; i < 6; i++)
        {
            lastResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "WrongPassword123"));
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse.StatusCode);
    }
}
