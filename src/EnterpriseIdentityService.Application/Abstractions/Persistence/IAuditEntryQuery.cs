using EnterpriseIdentityService.Application.Auditing;
using EnterpriseIdentityService.Domain.Auditing;
using EnterpriseIdentityService.Domain.Roles;
using EnterpriseIdentityService.Domain.Users;

namespace EnterpriseIdentityService.Application.Abstractions.Persistence;

public interface IAuditEntryQuery
{
    Task<AuditQuerySlice> QueryAsync(
        AuditQueryCriteria criteria,
        CancellationToken cancellationToken);
}

public sealed record AuditQueryCriteria(
    UserId? UserId,
    RoleId? RoleId,
    UserSessionId? SessionId,
    AuditEventType? EventType,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    string? CorrelationId,
    AuditCursor? Cursor,
    int PageSize);

public sealed record AuditQuerySlice(
    IReadOnlyList<AuditEntryResult> Items,
    bool HasMore);
