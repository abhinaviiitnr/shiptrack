using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Api.Middleware;

// WHY: One central error handler returns RFC 7807 ProblemDetails for every
// unhandled exception. No stack traces leak to the client in production.
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext ctx,
        Exception exception,
        CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            Application.Exceptions.ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
        };

        // Log the full exception internally but never expose it to the caller.
        logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);

        ctx.Response.StatusCode = status;
        await ctx.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status == 500 ? "Please contact support." : exception.Message,
            Extensions = { ["correlationId"] = ctx.TraceIdentifier }
        }, ct);

        return true;
    }
}
