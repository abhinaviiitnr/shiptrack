using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orders.Api.Application.Commands;
using Orders.Api.Application.DTOs;
using Orders.Api.Application.Queries;
using Orders.Api.Domain;
using Orders.Api.Infrastructure;

namespace Orders.Api.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize(Policy = "Authenticated")]
public sealed class OrdersController(
    CreateOrderHandler createHandler,
    GetOrderByIdQuery getByIdQuery,
    GetCustomerOrdersQuery getCustomerOrdersQuery,
    GetAdminOrdersQuery getAdminOrdersQuery,
    UpdateOrderStatusHandler updateStatusHandler,
    CancelOrderHandler cancelHandler,
    IValidator<CreateOrderRequest> createValidator) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<OrderDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest req, CancellationToken ct)
    {
        var validation = await createValidator.ValidateAsync(req, ct);
        if (!validation.IsValid)
        {
            var modelState = new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary();
            foreach (var err in validation.Errors) modelState.AddModelError(err.PropertyName, err.ErrorMessage);
            return ValidationProblem(modelState);
        }

        var customerId = GetUserId();
        var result = await createHandler.HandleAsync(req, customerId, ct);

        if (!result.IsSuccess)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Order creation failed",
                Detail = result.Detail,
                Extensions = { ["errorCode"] = result.ErrorCode, ["productId"] = result.ProductId }
            });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Order!.Id }, result.Order);
    }

    [HttpGet("mine")]
    [ProducesResponseType<PagedCursorResult<OrderSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyOrders(
        [FromQuery] Guid? cursor,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var customerId = GetUserId();
        var result = await getCustomerOrdersQuery.HandleAsync(customerId, cursor, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var isAdmin = User.IsInRole("Admin");
        var order = await getByIdQuery.HandleAsync(id, userId, isAdmin, ct);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType<PagedCursorResult<OrderSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAdminOrders(
        [FromQuery] OrderStatus? status,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? customerId,
        [FromQuery] Guid? cursor,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var filter = new AdminOrderFilter(status, from, to, customerId, cursor, pageSize);
        var result = await getAdminOrdersQuery.HandleAsync(filter, ct);
        return Ok(result);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateStatus(
        Guid id, [FromBody] UpdateStatusRequest req, CancellationToken ct)
    {
        var changedBy = User.FindFirstValue(ClaimTypes.Email) ?? GetUserId().ToString();
        var result = await updateStatusHandler.HandleAsync(id, req.Status, changedBy, ct);

        if (result.IsNotFound) return NotFound();
        if (!result.IsSuccess)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Invalid status transition",
                Detail = result.ErrorDetail
            });
        }

        return NoContent();
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var isAdmin = User.IsInRole("Admin");
        var result = await cancelHandler.HandleAsync(id, userId, isAdmin, ct);

        if (result.IsNotFound) return NotFound();
        if (result.IsForbidden) return Forbid();
        if (!result.IsSuccess)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Cannot cancel order",
                Detail = result.ErrorDetail
            });
        }

        return NoContent();
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new InvalidOperationException("User ID claim not found.");
        return Guid.Parse(sub);
    }
}
