namespace EnterpriseIdentityService.Application.Auditing;

public sealed record AuditEntryResult(
    Guid Id,
    string EventType,
    string Outcome,
    string? ReasonCode,
    DateTimeOffset OccurredAtUtc,
    Guid? ActorUserId,
    Guid? TargetUserId,
    Guid? RoleId,
    Guid? SessionId,
    string CorrelationId,
    string? IpAddress,
    string? UserAgent,
    string? Permission);
