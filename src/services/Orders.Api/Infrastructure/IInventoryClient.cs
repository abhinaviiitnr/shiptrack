using Orders.Api.Application.DTOs;

namespace Orders.Api.Infrastructure;

// WHY: Typed HttpClient for Inventory lets us configure retries, circuit breakers
// and timeouts in one place. Using an interface means we can substitute a fake
// in unit tests without spinning up the actual Inventory service.
public interface IInventoryClient
{
    Task<IReadOnlyList<ProductPriceDto>> GetProductPricesAsync(
        IEnumerable<Guid> ids, CancellationToken ct = default);

    Task<InventoryReserveResult> ReserveAsync(
        IReadOnlyList<ReserveItemDto> items, CancellationToken ct = default);

    Task ReleaseAsync(
        IReadOnlyList<ReserveItemDto> items, CancellationToken ct = default);
}
