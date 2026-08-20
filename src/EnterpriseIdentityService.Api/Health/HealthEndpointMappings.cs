using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;

namespace EnterpriseIdentityService.Api.Health;

internal static class HealthEndpointMappings
{
    private const string ReadinessTag = "ready";

    public static IEndpointRouteBuilder MapOperationalHealthEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
            {
                Predicate = _ => false,
                ResponseWriter = WriteMinimalResponseAsync
            })
            .WithName("Liveness")
            .AllowAnonymous()
            .DisableRateLimiting();

        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
            {
                Predicate = registration => registration.Tags.Contains(ReadinessTag),
                ResponseWriter = WriteMinimalResponseAsync
            })
            .WithName("Readiness")
            .AllowAnonymous()
            .DisableRateLimiting();

        return endpoints;
    }

    private static Task WriteMinimalResponseAsync(
        HttpContext context,
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthReport report)
    {
        return context.Response.WriteAsJsonAsync(
            new Dictionary<string, string>
            {
                ["status"] = report.Status.ToString()
            },
            cancellationToken: context.RequestAborted);
    }
}
