using EnterpriseIdentityService.Application.Abstractions.Auditing;
using EnterpriseIdentityService.Domain.Auditing;

namespace EnterpriseIdentityService.Api.Auditing;

internal sealed class HttpAuditContextProvider(IHttpContextAccessor httpContextAccessor)
    : IAuditContextProvider
{
    public AuditContext GetCurrent()
    {
        HttpContext? httpContext = httpContextAccessor.HttpContext;
        string correlationId = Bounded(
            httpContext?.TraceIdentifier,
            AuditEntry.MaximumCorrelationIdLength) ?? Guid.NewGuid().ToString("N");
        string? ipAddress = Bounded(
            httpContext?.Connection.RemoteIpAddress?.ToString(),
            AuditEntry.MaximumIpAddressLength);
        string? userAgent = Bounded(
            httpContext?.Request.Headers.UserAgent.ToString(),
            AuditEntry.MaximumUserAgentLength);

        return new AuditContext(correlationId, ipAddress, userAgent);
    }

    private static string? Bounded(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        return trimmed.Length <= maximumLength ? trimmed : trimmed[..maximumLength];
    }
}
