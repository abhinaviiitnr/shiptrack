using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Inventory.Api.Application.DTOs;
using Inventory.Api.Domain;
using Inventory.Api.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;
using Xunit;

namespace Inventory.Tests.Integration;

public class InventoryIntegrationTests : IAsyncLifetime
{
    private const string TestJwtSecret = "fallback-test-secret-key-at-least-32-chars!!";
    private const string InternalKey = "dev-internal-key";

    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("inventory_test_db")
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
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Secret"] = TestJwtSecret,
                    ["Jwt:Issuer"] = "ShipTrack",
                    ["Jwt:Audience"] = "ShipTrackClient",
                    ["InternalKey:Value"] = InternalKey
                });
            });
            builder.ConfigureTestServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<InventoryDbContext>));
                if (descriptor is not null) services.Remove(descriptor);

                services.AddDbContext<InventoryDbContext>(options =>
                    options.UseNpgsql(_dbContainer.GetConnectionString()));
            });
        });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await db.Database.MigrateAsync();

        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _dbContainer.DisposeAsync();
    }

    private static string GenerateJwt(string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, role)
        };

        var token = new JwtSecurityToken(
            issuer: "ShipTrack",
            audience: "ShipTrackClient",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public async Task Products_Get_Returns200_ForAuthenticatedUser()
    {
        var token = GenerateJwt(Roles.Customer);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/products");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Products_Post_Returns403_ForCustomer()
    {
        var token = GenerateJwt(Roles.Customer);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var req = new CreateProductRequest("SKU-CUST", "Hacker Mouse", "Forbidden", 20m, 5);
        var response = await _client.PostAsJsonAsync("/api/products", req);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Products_Post_Returns201_ForAdmin()
    {
        var token = GenerateJwt(Roles.Admin);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var sku = $"SKU-ADM-{Guid.NewGuid():N}";
        var req = new CreateProductRequest(sku, "Admin Monitor", "4K HDR", 399.99m, 15);
        var response = await _client.PostAsJsonAsync("/api/products", req);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ProductDto>();
        Assert.NotNull(created);
        Assert.Equal(sku, created.Sku);
        Assert.Equal(15, created.StockQuantity);
    }

    [Fact]
    public async Task InternalEndpoints_WithoutKey_ReturnsForbidden()
    {
        var req = new ReserveRequest([new ReserveItem(Guid.NewGuid(), 1)]);
        var response = await _client.PostAsJsonAsync("/api/inventory/reserve", req);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ReserveStock_ParallelRequestsForLastUnit_ExactlyOneSucceeds()
    {
        // 1. Seed product with exactly 1 unit of stock
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var product = new Product
        {
            Sku = $"SKU-CONC-{Guid.NewGuid():N}",
            Name = "Limited Edition GPU",
            Price = 999.99m,
            StockQuantity = 1
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var client1 = _factory.CreateClient();
        client1.DefaultRequestHeaders.Add("X-Internal-Key", InternalKey);

        var client2 = _factory.CreateClient();
        client2.DefaultRequestHeaders.Add("X-Internal-Key", InternalKey);

        var reserveReq = new ReserveRequest([new ReserveItem(product.Id, 1)]);

        // 2. Fire two parallel requests simultaneously using Task.WhenAll
        var task1 = client1.PostAsJsonAsync("/api/inventory/reserve", reserveReq);
        var task2 = client2.PostAsJsonAsync("/api/inventory/reserve", reserveReq);

        var responses = await Task.WhenAll(task1, task2);

        // 3. Exactly one MUST be 200 OK, and exactly one MUST be 409 Conflict
        var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        var conflictCount = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(1, successCount);
        Assert.Equal(1, conflictCount);

        // 4. Verify remaining stock in database is exactly 0 (no overselling)
        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var finalProduct = await verifyDb.Products.AsNoTracking().FirstAsync(p => p.Id == product.Id);
        Assert.Equal(0, finalProduct.StockQuantity);
    }

    [Fact]
    public async Task ReserveStock_DuplicateLines_CannotOversell()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var product = new Product
        {
            Sku = $"SKU-DUP-{Guid.NewGuid():N}",
            Name = "Limited duplicate-line product",
            Price = 10m,
            StockQuantity = 2
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Internal-Key", InternalKey);
        var request = new ReserveRequest(
        [
            new ReserveItem(product.Id, 1),
            new ReserveItem(product.Id, 2)
        ]);

        var response = await client.PostAsJsonAsync("/api/inventory/reserve", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var unchanged = await verifyDb.Products.AsNoTracking().SingleAsync(p => p.Id == product.Id);
        Assert.Equal(2, unchanged.StockQuantity);
    }
}
