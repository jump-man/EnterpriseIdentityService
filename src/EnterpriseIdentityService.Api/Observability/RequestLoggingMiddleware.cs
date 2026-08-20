using System.Diagnostics;

namespace EnterpriseIdentityService.Api.Observability;

internal sealed class RequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        long startedAt = Stopwatch.GetTimestamp();

        try
        {
            await next(context);
        }
        catch
        {
            // The global exception handler owns the single diagnostic exception log.
            throw;
        }

        string route = RequestMetadata.Route(context);
        if (RequestMetadata.IsHealthRoute(route) && context.Response.StatusCode < 500)
        {
            return;
        }

        double elapsedMilliseconds = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        string method = RequestMetadata.Method(context);
        string correlationId = CorrelationId.GetOrCreate(context);
        string traceId = Activity.Current?.TraceId.ToString() ?? string.Empty;

        if (context.Response.StatusCode >= 500)
        {
            logger.LogWarning(
                "Request {RequestMethod} {RequestRoute} completed with status code {StatusCode} in {ElapsedMilliseconds} ms using correlation {CorrelationId} and trace {TraceId}",
                method,
                route,
                context.Response.StatusCode,
                elapsedMilliseconds,
                correlationId,
                traceId);
            return;
        }

        logger.LogInformation(
            "Request {RequestMethod} {RequestRoute} completed with status code {StatusCode} in {ElapsedMilliseconds} ms using correlation {CorrelationId} and trace {TraceId}",
            method,
            route,
            context.Response.StatusCode,
            elapsedMilliseconds,
            correlationId,
            traceId);
    }
}
