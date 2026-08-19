using EnterpriseIdentityService.Application.Abstractions.Auditing;
using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Domain.Auditing;
using EnterpriseIdentityService.Domain.Roles;
using EnterpriseIdentityService.Domain.Users;

namespace EnterpriseIdentityService.Application.Auditing;

public sealed class AuditRecorder(
    IAuditEntryRepository auditEntries,
    IAuditContextProvider contextProvider,
    TimeProvider timeProvider)
{
    public AuditEntry Record(
        AuditEventType eventType,
        AuditOutcome outcome = AuditOutcome.Success,
        AuditReasonCode? reasonCode = null,
        UserId? actorUserId = null,
        UserId? targetUserId = null,
        RoleId? roleId = null,
        UserSessionId? sessionId = null,
        string? permission = null)
    {
        AuditContext context = contextProvider.GetCurrent();
        AuditEntry entry = AuditEntry.Create(
            AuditEntryId.New(),
            eventType,
            outcome,
            reasonCode,
            timeProvider.GetUtcNow(),
            context.CorrelationId,
            actorUserId,
            targetUserId,
            roleId,
            sessionId,
            context.IpAddress,
            context.UserAgent,
            permission);
        auditEntries.Add(entry);
        return entry;
    }
}
