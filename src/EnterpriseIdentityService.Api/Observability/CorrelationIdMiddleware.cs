namespace EnterpriseIdentityService.Api.Observability;

internal sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId = CorrelationId.GetOrCreate(context);
        context.Response.OnStarting(static state =>
        {
            var (response, value) = ((HttpResponse Response, string Value))state;
            response.Headers[CorrelationId.HeaderName] = value;
            return Task.CompletedTask;
        }, (context.Response, correlationId));

        using IDisposable? scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId,
            ["TraceIdentifier"] = context.TraceIdentifier
        });

        await next(context);
    }
}
