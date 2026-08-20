using Microsoft.Extensions.Primitives;

namespace EnterpriseIdentityService.Api.Observability;

internal static class CorrelationId
{
    public const string HeaderName = "X-Correlation-ID";
    public const int MaximumLength = 64;

    private static readonly object HttpContextItemKey = new();

    public static string GetOrCreate(HttpContext context)
    {
        if (context.Items.TryGetValue(HttpContextItemKey, out object? existing) &&
            existing is string value)
        {
            return value;
        }

        string correlationId = TryGetIncoming(context.Request.Headers, out string incoming)
            ? incoming
            : Guid.NewGuid().ToString("N");
        context.Items[HttpContextItemKey] = correlationId;
        return correlationId;
    }

    public static string? GetCurrent(HttpContext? context) =>
        context is not null &&
        context.Items.TryGetValue(HttpContextItemKey, out object? value) &&
        value is string correlationId
            ? correlationId
            : null;

    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string candidate = value;
        if (candidate.Length is < 1 or > MaximumLength ||
            candidate.Any(character => !IsAllowed(character)))
        {
            return false;
        }

        normalized = candidate;
        return true;
    }

    private static bool TryGetIncoming(
        IHeaderDictionary headers,
        out string correlationId)
    {
        correlationId = string.Empty;
        return headers.TryGetValue(HeaderName, out StringValues values) &&
            values.Count == 1 &&
            TryNormalize(values[0], out correlationId);
    }

    private static bool IsAllowed(char character) =>
        character is >= 'a' and <= 'z' or
            >= 'A' and <= 'Z' or
            >= '0' and <= '9' or
            '-' or '_' or '.';
}
