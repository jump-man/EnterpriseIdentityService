namespace EnterpriseIdentityService.Application.Abstractions.Auditing;

public sealed record AuditContext(
    string CorrelationId,
    string? IpAddress,
    string? UserAgent);
