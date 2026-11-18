using Inventory.Api.Application.Commands;
using Inventory.Api.Application.DTOs;
using Inventory.Api.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Inventory.Api.Api.Controllers;

// WHY: Internal endpoints are NOT registered in the Gateway routes.
// They are secured by a shared secret header instead of JWT so that
// service-to-service calls don't need to exchange tokens.
[ApiController]
[Route("api/inventory")]
public sealed class InventoryInternalController(
    ReserveStockHandler reserveHandler,
    ReleaseStockHandler releaseHandler,
    IOptions<InternalKeyOptions> internalKey) : ControllerBase
{
    [HttpPost("reserve")]
    [ProducesResponseType(200)]
    [ProducesResponseType<ProblemDetails>(409)]
    public async Task<IActionResult> Reserve([FromBody] ReserveRequest req, CancellationToken ct)
    {
        if (!IsAuthorized()) return Forbid();

        var result = await reserveHandler.HandleAsync(req, ct);
        if (!result.IsSuccess)
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Stock reservation failed",
                Detail = result.Detail,
                Extensions = { ["errorCode"] = result.ErrorCode, ["productId"] = result.ProductId }
            });

        return Ok();
    }

    [HttpPost("release")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> Release([FromBody] ReleaseRequest req, CancellationToken ct)
    {
        if (!IsAuthorized()) return Forbid();
        await releaseHandler.HandleAsync(req, ct);
        return Ok();
    }

    private bool IsAuthorized()
    {
        // Validate the internal shared secret header.
        return Request.Headers.TryGetValue("X-Internal-Key", out var key)
               && key == internalKey.Value.Value;
    }
}
