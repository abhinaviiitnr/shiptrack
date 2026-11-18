using FluentValidation;
using Identity.Api.Application.Commands;
using Identity.Api.Application.DTOs;
using Identity.Api.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Api.Controllers;

[ApiController]
[Route("api/auth")]
// WHY: Controllers only receive HTTP requests, validate input and delegate
// to handlers. No business logic lives here.
public sealed class AuthController(
    RegisterCommandHandler registerHandler,
    LoginCommandHandler loginHandler,
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator,
    ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken ct)
    {
        var validation = await registerValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var modelState = new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary();
            foreach (var err in validation.Errors)
                modelState.AddModelError(err.PropertyName, err.ErrorMessage);
            return ValidationProblem(modelState);
        }

        var response = await registerHandler.HandleAsync(request, ct);
        return CreatedAtAction(nameof(Register), response);
    }

    [HttpPost("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        var validation = await loginValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            // WHY: Return a generic failure, not validation details, on login.
            // Don't reveal whether the email exists.
            return Unauthorized(new ProblemDetails
            {
                Status = 401,
                Title = "Invalid credentials",
                Detail = "Email or password is incorrect."
            });

        var response = await loginHandler.HandleAsync(request, ct);
        if (response is null)
        {
            // Generic message — never reveal which field was wrong.
            logger.LogWarning("Failed login attempt for email {Email}", request.Email);
            return Unauthorized(new ProblemDetails
            {
                Status = 401,
                Title = "Invalid credentials",
                Detail = "Email or password is incorrect."
            });
        }

        return Ok(response);
    }
}
