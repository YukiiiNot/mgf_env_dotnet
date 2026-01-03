namespace MGF.Api.Middleware;

using Microsoft.Extensions.Primitives;

public sealed class ApiKeyMiddleware
{
    private const string HeaderName = "X-MGF-API-KEY";

    private readonly RequestDelegate next;
    private readonly IConfiguration configuration;
    private readonly ILogger<ApiKeyMiddleware> logger;

    public ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<ApiKeyMiddleware> logger)
    {
        this.next = next;
        this.configuration = configuration;
        this.logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await next(context);
            return;
        }

        var expected = configuration["Security:ApiKey"];
        if (string.IsNullOrWhiteSpace(expected))
        {
            logger.LogWarning("MGF.Api: missing config value Security:ApiKey; rejecting all /api requests.");
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out StringValues provided) || StringValues.IsNullOrEmpty(provided))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Missing API key." });
            return;
        }

        if (!string.Equals(provided.ToString(), expected, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid API key." });
            return;
        }

        await next(context);
    }
}

