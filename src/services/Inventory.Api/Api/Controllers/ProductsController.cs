using FluentValidation;
using Inventory.Api.Application.Commands;
using Inventory.Api.Application.DTOs;
using Inventory.Api.Application.Queries;
using Inventory.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Api.Controllers;

[ApiController]
[Route("api/products")]
[Authorize(Policy = "Authenticated")]
public sealed class ProductsController(
    GetProductsQuery getProducts,
    GetProductByIdQuery getById,
    GetProductBatchQuery getBatch,
    CreateProductHandler createProduct,
    UpdateProductHandler updateProduct,
    DeleteProductHandler deleteProduct,
    IValidator<CreateProductRequest> createValidator,
    IValidator<UpdateProductRequest> updateValidator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<ProductDto>>(200)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? name,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 20;
        var result = await getProducts.HandleAsync(name, page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProductDto>(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var product = await getById.HandleAsync(id, ct);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpGet("batch")]
    [ProducesResponseType<IReadOnlyList<ProductDto>>(200)]
    public async Task<IActionResult> GetBatch([FromQuery] Guid[] ids, CancellationToken ct)
    {
        var products = await getBatch.HandleAsync(ids, ct);
        return Ok(products);
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType<ProductDto>(201)]
    [ProducesResponseType<ValidationProblemDetails>(400)]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest req, CancellationToken ct)
    {
        var v = await createValidator.ValidateAsync(req, ct);
        if (!v.IsValid)
        {
            var modelState = new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary();
            foreach (var err in v.Errors) modelState.AddModelError(err.PropertyName, err.ErrorMessage);
            return ValidationProblem(modelState);
        }

        var product = await createProduct.HandleAsync(req, ct);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType<ProductDto>(200)]
    [ProducesResponseType<ValidationProblemDetails>(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductRequest req, CancellationToken ct)
    {
        var v = await updateValidator.ValidateAsync(req, ct);
        if (!v.IsValid)
        {
            var modelState = new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary();
            foreach (var err in v.Errors) modelState.AddModelError(err.PropertyName, err.ErrorMessage);
            return ValidationProblem(modelState);
        }

        var product = await updateProduct.HandleAsync(id, req, ct);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var deleted = await deleteProduct.HandleAsync(id, ct);
        return deleted ? NoContent() : NotFound();
    }
}
