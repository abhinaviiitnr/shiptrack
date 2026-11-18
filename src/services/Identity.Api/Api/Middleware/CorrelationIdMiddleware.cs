namespace Identity.Api.Api.Middleware;

// WHY: Correlation IDs tie together log lines across multiple services.
// Without them, debugging a distributed request is nearly impossible.
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext ctx)
    {
        // Accept an incoming correlation ID (e.g. from the Gateway or a test)
        // or create a new one if none is supplied.
        var correlationId = ctx.Request.Headers.TryGetValue(HeaderName, out var value)
            ? value.ToString()
            : Guid.NewGuid().ToString();

        ctx.Items[HeaderName] = correlationId;
        ctx.Response.Headers[HeaderName] = correlationId;

        await next(ctx);
    }
}
