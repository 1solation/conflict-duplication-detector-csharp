namespace ConflictDuplicationDetector.Api.Middleware;

public class ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
{
    private const string ApiKeyHeader = "X-Api-Key";

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsExempt(context.Request.Path))
        {
            await next(context);
            return;
        }

        var configuredKey = configuration["Auth:ApiKey"];

        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            await next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(ApiKeyHeader, out var providedKey)
            || !string.Equals(providedKey, configuredKey, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://tools.ietf.org/html/rfc9110#section-15.5.2",
                title = "Unauthorized",
                status = 401,
                detail = "A valid API key must be provided in the X-Api-Key header."
            });
            return;
        }

        await next(context);
    }

    private static bool IsExempt(PathString path)
    {
        if (!path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            return true;

        return path.StartsWithSegments("/api/health", StringComparison.OrdinalIgnoreCase);
    }
}
