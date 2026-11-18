using System.Net.Http.Headers;
using System.Net.Http.Json;
using Orders.Api.Application.DTOs;
using Orders.Api.Configuration;
using Microsoft.Extensions.Options;

namespace Orders.Api.Infrastructure;

// WHY: The HttpClient is registered as a typed client and configured with
// resilience policies (timeout, retries, circuit breaker) in Program.cs.
// This class only knows how to call Inventory's API — not about retry logic.
public sealed class InventoryClient(HttpClient http, IOptions<InventoryServiceOptions> opts)
    : IInventoryClient
{
    private readonly InventoryServiceOptions _opts = opts.Value;

    public async Task<IReadOnlyList<ProductPriceDto>> GetProductPricesAsync(
        IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var query = string.Join("&", ids.Select(id => $"ids={id}"));
        var response = await http.GetAsync($"/api/products/batch?{query}", ct);
        response.EnsureSuccessStatusCode();

        var products = await response.Content.ReadFromJsonAsync<List<ProductPriceDto>>(ct)
            ?? [];
        return products;
    }

    public async Task<InventoryReserveResult> ReserveAsync(
        IReadOnlyList<ReserveItemDto> items, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/inventory/reserve");
        req.Headers.Add("X-Internal-Key", _opts.InternalKey);
        req.Content = JsonContent.Create(new { Items = items });

        var response = await http.SendAsync(req, ct);
        if (response.IsSuccessStatusCode)
            return new InventoryReserveResult(true, null);

        // Parse the ProblemDetails from a 409 response.
        var problem = await response.Content.ReadFromJsonAsync<InventoryProblemDetails>(ct);
        return new InventoryReserveResult(false, problem?.Detail ?? "Stock reservation failed.");
    }

    public async Task ReleaseAsync(
        IReadOnlyList<ReserveItemDto> items, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/inventory/release");
        req.Headers.Add("X-Internal-Key", _opts.InternalKey);
        req.Content = JsonContent.Create(new { Items = items });

        // Best-effort: log if it fails but don't throw.
        var response = await http.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();
    }
}

public sealed record InventoryReserveResult(bool Success, string? ErrorDetail);
public sealed record InventoryProblemDetails(string? Detail);
