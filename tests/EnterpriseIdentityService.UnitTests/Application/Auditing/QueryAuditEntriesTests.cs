using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Application.Auditing;
using EnterpriseIdentityService.Domain.Auditing;

namespace EnterpriseIdentityService.UnitTests.Application.Auditing;

public sealed class QueryAuditEntriesTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 19, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_ShouldApplyBoundedDefaultsAndEncodeNextCursor()
    {
        AuditEntryResult item = Entry(Now.AddMinutes(-1));
        var query = new FakeQuery(new AuditQuerySlice([item], true));
        var handler = new QueryAuditEntriesQueryHandler(query, new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new QueryAuditEntriesQuery(null, null, null, null, null, null, null, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.NextCursor);
        Assert.Equal(Now.AddDays(-30), query.Criteria!.FromUtc);
        Assert.Equal(Now, query.Criteria.ToUtc);
        Assert.Equal(50, query.Criteria.PageSize);
        Assert.True(AuditCursor.TryDecode(result.Value.NextCursor, out AuditCursor? cursor));
        Assert.Equal(item.Id, cursor!.Id.Value);
    }

    [Theory]
    [InlineData(0, "Audit.InvalidPageSize")]
    [InlineData(101, "Audit.InvalidPageSize")]
    public async Task Handle_ShouldRejectInvalidPageSize(int pageSize, string errorCode)
    {
        var handler = Handler();
        var result = await handler.Handle(
            new QueryAuditEntriesQuery(null, null, null, null, null, null, null, null, pageSize),
            CancellationToken.None);
        Assert.Equal(errorCode, result.Error.Code);
    }

    [Fact]
    public async Task Handle_ShouldRejectUnknownEventInvalidCursorAndOversizedRange()
    {
        QueryAuditEntriesQueryHandler handler = Handler();

        var unknown = await handler.Handle(new(
            null, null, null, "UnknownEvent", null, null, null, null), CancellationToken.None);
        var cursor = await handler.Handle(new(
            null, null, null, null, null, null, null, "not-a-cursor"), CancellationToken.None);
        var range = await handler.Handle(new(
            null, null, null, null, Now.AddDays(-91), Now, null, null), CancellationToken.None);

        Assert.Equal(AuditQueryErrors.InvalidEventType, unknown.Error);
        Assert.Equal(AuditQueryErrors.InvalidCursor, cursor.Error);
        Assert.Equal(AuditQueryErrors.InvalidTimeRange, range.Error);
    }

    private static QueryAuditEntriesQueryHandler Handler() =>
        new(new FakeQuery(new AuditQuerySlice([], false)), new FixedTimeProvider(Now));

    private static AuditEntryResult Entry(DateTimeOffset occurredAtUtc) => new(
        Guid.NewGuid(), "LoginSucceeded", "Success", null, occurredAtUtc,
        null, null, null, null, "correlation", null, null, null);

    private sealed class FakeQuery(AuditQuerySlice result) : IAuditEntryQuery
    {
        public AuditQueryCriteria? Criteria { get; private set; }

        public Task<AuditQuerySlice> QueryAsync(
            AuditQueryCriteria criteria,
            CancellationToken cancellationToken)
        {
            Criteria = criteria;
            return Task.FromResult(result);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
