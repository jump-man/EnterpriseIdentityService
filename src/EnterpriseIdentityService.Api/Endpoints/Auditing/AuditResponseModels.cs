namespace EnterpriseIdentityService.Api.Endpoints.Auditing;

internal sealed record AuditEntryResponse(
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

internal sealed record AuditPageResponse(
    IReadOnlyList<AuditEntryResponse> Items,
    string? NextCursor);
