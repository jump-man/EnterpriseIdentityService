using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Application.Auditing;
using EnterpriseIdentityService.Domain.Auditing;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseIdentityService.Infrastructure.Persistence.Queries;

internal sealed class AuditEntryQuery(ApplicationDbContext dbContext) : IAuditEntryQuery
{
    public async Task<AuditQuerySlice> QueryAsync(
        AuditQueryCriteria criteria,
        CancellationToken cancellationToken)
    {
        IQueryable<AuditEntry> query = dbContext.AuditEntries
            .AsNoTracking()
            .Where(entry =>
                entry.OccurredAtUtcTicks >= criteria.FromUtc.UtcTicks &&
                entry.OccurredAtUtcTicks <= criteria.ToUtc.UtcTicks);

        if (criteria.UserId is not null)
        {
            query = query.Where(entry =>
                entry.ActorUserId == criteria.UserId ||
                entry.TargetUserId == criteria.UserId);
        }

        if (criteria.RoleId is not null)
        {
            query = query.Where(entry => entry.RoleId == criteria.RoleId);
        }

        if (criteria.SessionId is not null)
        {
            query = query.Where(entry => entry.SessionId == criteria.SessionId);
        }

        if (criteria.EventType is not null)
        {
            query = query.Where(entry => entry.EventType == criteria.EventType);
        }

        if (criteria.CorrelationId is not null)
        {
            query = query.Where(entry => entry.CorrelationId == criteria.CorrelationId);
        }

        if (criteria.Cursor is not null)
        {
            long cursorTicks = criteria.Cursor.OccurredAtUtc.UtcTicks;
            string cursorSortId = criteria.Cursor.Id.Value.ToString("N");
            query = query.Where(entry =>
                entry.OccurredAtUtcTicks < cursorTicks ||
                (entry.OccurredAtUtcTicks == cursorTicks &&
                 string.Compare(entry.SortId, cursorSortId) < 0));
        }

        List<AuditEntry> entries = await query
            .OrderByDescending(entry => entry.OccurredAtUtcTicks)
            .ThenByDescending(entry => entry.SortId)
            .Take(criteria.PageSize + 1)
            .ToListAsync(cancellationToken);

        bool hasMore = entries.Count > criteria.PageSize;
        if (hasMore)
        {
            entries.RemoveAt(entries.Count - 1);
        }

        return new AuditQuerySlice(
            entries.Select(entry => new AuditEntryResult(
                entry.Id.Value,
                entry.EventType.ToString(),
                entry.Outcome.ToString(),
                entry.ReasonCode?.ToString(),
                entry.OccurredAtUtc,
                entry.ActorUserId?.Value,
                entry.TargetUserId?.Value,
                entry.RoleId?.Value,
                entry.SessionId?.Value,
                entry.CorrelationId,
                entry.IpAddress,
                entry.UserAgent,
                entry.Permission)).ToArray(),
            hasMore);
    }
}
