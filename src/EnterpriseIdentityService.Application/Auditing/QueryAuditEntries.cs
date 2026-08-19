using EnterpriseIdentityService.Application.Abstractions;
using EnterpriseIdentityService.Application.Abstractions.Messaging;
using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Domain.Auditing;
using EnterpriseIdentityService.Domain.Roles;
using EnterpriseIdentityService.Domain.Users;

namespace EnterpriseIdentityService.Application.Auditing;

public sealed record QueryAuditEntriesQuery(
    UserId? UserId,
    RoleId? RoleId,
    UserSessionId? SessionId,
    string? EventType,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? CorrelationId,
    string? Cursor,
    int PageSize = 50) : ICommand<AuditPageResult>;

public sealed record AuditPageResult(
    IReadOnlyList<AuditEntryResult> Items,
    string? NextCursor);

public sealed class QueryAuditEntriesQueryHandler(
    IAuditEntryQuery auditEntries,
    TimeProvider timeProvider)
    : ICommandHandler<QueryAuditEntriesQuery, AuditPageResult>
{
    private static readonly TimeSpan DefaultRange = TimeSpan.FromDays(30);
    private static readonly TimeSpan MaximumRange = TimeSpan.FromDays(90);

    public async Task<Result<AuditPageResult>> Handle(
        QueryAuditEntriesQuery query,
        CancellationToken cancellationToken)
    {
        if (query.PageSize is < 1 or > 100)
        {
            return Result<AuditPageResult>.Failure(AuditQueryErrors.InvalidPageSize);
        }

        AuditEventType? eventType = null;
        if (query.EventType is not null)
        {
            if (!Enum.TryParse(query.EventType, false, out AuditEventType parsed) ||
                !Enum.IsDefined(parsed))
            {
                return Result<AuditPageResult>.Failure(AuditQueryErrors.InvalidEventType);
            }

            eventType = parsed;
        }

        DateTimeOffset toUtc = (query.To ?? timeProvider.GetUtcNow()).ToUniversalTime();
        DateTimeOffset fromUtc = (query.From ?? toUtc.Subtract(DefaultRange)).ToUniversalTime();
        if (fromUtc >= toUtc || toUtc - fromUtc > MaximumRange)
        {
            return Result<AuditPageResult>.Failure(AuditQueryErrors.InvalidTimeRange);
        }

        string? correlationId = query.CorrelationId?.Trim();
        if (query.CorrelationId is not null &&
            (string.IsNullOrWhiteSpace(correlationId) ||
             correlationId.Length > AuditEntry.MaximumCorrelationIdLength))
        {
            return Result<AuditPageResult>.Failure(AuditQueryErrors.InvalidCorrelationId);
        }

        AuditCursor? cursor = null;
        if (query.Cursor is not null && !AuditCursor.TryDecode(query.Cursor, out cursor))
        {
            return Result<AuditPageResult>.Failure(AuditQueryErrors.InvalidCursor);
        }

        AuditQuerySlice slice = await auditEntries.QueryAsync(
            new AuditQueryCriteria(
                query.UserId,
                query.RoleId,
                query.SessionId,
                eventType,
                fromUtc,
                toUtc,
                correlationId,
                cursor,
                query.PageSize),
            cancellationToken);

        string? nextCursor = slice.HasMore && slice.Items.Count > 0
            ? new AuditCursor(
                slice.Items[^1].OccurredAtUtc,
                new AuditEntryId(slice.Items[^1].Id)).Encode()
            : null;
        return Result<AuditPageResult>.Success(new AuditPageResult(slice.Items, nextCursor));
    }
}
