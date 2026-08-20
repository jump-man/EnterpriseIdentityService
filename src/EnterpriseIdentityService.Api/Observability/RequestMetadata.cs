using Microsoft.AspNetCore.Routing;

namespace EnterpriseIdentityService.Api.Observability;

internal static class RequestMetadata
{
    private const int MaximumMethodLength = 16;
    private const int MaximumRouteLength = 200;

    public static string Method(HttpContext context) =>
        Bounded(context.Request.Method, MaximumMethodLength, "UNKNOWN");

    public static string Route(HttpContext context)
    {
        string? route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
        return Bounded(route, MaximumRouteLength, "<unmatched>");
    }

    public static bool IsHealthRoute(string route) =>
        route is "/health/live" or "/health/ready";

    private static string Bounded(
        string? value,
        int maximumLength,
        string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        string trimmed = value.Trim();
        return trimmed.Length <= maximumLength
            ? trimmed
            : trimmed[..maximumLength];
    }
}
