using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Application.Auditing;
using EnterpriseIdentityService.Domain.Auditing;
using EnterpriseIdentityService.Domain.Roles;
using EnterpriseIdentityService.Domain.Users;
using EnterpriseIdentityService.Infrastructure.Persistence;
using EnterpriseIdentityService.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseIdentityService.IntegrationTests.Infrastructure.Persistence;

public sealed class AuditPersistenceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 19, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SaveChanges_ShouldRoundTripControlledAuditFields()
    {
        await using SqliteTestDatabase database = await SqliteTestDatabase.CreateAsync();
        UserId actor = UserId.New();
        UserId target = UserId.New();
        RoleId role = RoleId.New();
        UserSessionId session = UserSessionId.New();
        AuditEntry entry = CreateEntry(
            AuditEntryId.New(), AuditEventType.RoleAssignedToUser,
            actor, target, role, session, "roles.read");

        await using (ApplicationDbContext write = database.CreateContext())
        {
            write.Set<AuditEntry>().Add(entry);
            await write.SaveChangesAsync();
        }

        await using ApplicationDbContext read = database.CreateContext();
        AuditEntry persisted = await read.Set<AuditEntry>().SingleAsync();
        Assert.Equal(entry.Id, persisted.Id);
        Assert.Equal(actor, persisted.ActorUserId);
        Assert.Equal(target, persisted.TargetUserId);
        Assert.Equal(role, persisted.RoleId);
        Assert.Equal(session, persisted.SessionId);
        Assert.Equal(Now, persisted.OccurredAtUtc);
        Assert.Equal("roles.read", persisted.Permission);
    }

    [Fact]
    public async Task SaveChanges_ShouldRejectAuditModificationAndDeletion()
    {
        await using SqliteTestDatabase database = await SqliteTestDatabase.CreateAsync();
        AuditEntry entry = CreateEntry(AuditEntryId.New(), AuditEventType.LoginSucceeded);
        await using (ApplicationDbContext seed = database.CreateContext())
        {
            seed.Set<AuditEntry>().Add(entry);
            await seed.SaveChangesAsync();
        }

        await using (ApplicationDbContext modify = database.CreateContext())
        {
            AuditEntry existing = await modify.Set<AuditEntry>().SingleAsync();
            modify.Entry(existing).Property(item => item.CorrelationId).CurrentValue = "changed";
            await Assert.ThrowsAsync<InvalidOperationException>(() => modify.SaveChangesAsync());
        }

        await using (ApplicationDbContext delete = database.CreateContext())
        {
            AuditEntry existing = await delete.Set<AuditEntry>().SingleAsync();
            delete.Set<AuditEntry>().Remove(existing);
            await Assert.ThrowsAsync<InvalidOperationException>(() => delete.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task HistoricalRoleReference_ShouldSurviveRoleDeletion()
    {
        await using SqliteTestDatabase database = await SqliteTestDatabase.CreateAsync();
        Role role = Role.Create(RoleId.New(), "Temporary");
        AuditEntry entry = CreateEntry(
            AuditEntryId.New(), AuditEventType.RoleCreated, roleId: role.Id);
        await using (ApplicationDbContext seed = database.CreateContext())
        {
            seed.Set<Role>().Add(role);
            seed.Set<AuditEntry>().Add(entry);
            await seed.SaveChangesAsync();
        }

        await using (ApplicationDbContext remove = database.CreateContext())
        {
            Role existing = await remove.Set<Role>().SingleAsync(item => item.Id == role.Id);
            remove.Set<Role>().Remove(existing);
            await remove.SaveChangesAsync();
        }

        await using ApplicationDbContext verify = database.CreateContext();
        Assert.False(await verify.Set<Role>().AnyAsync(item => item.Id == role.Id));
        Assert.Equal(role.Id, (await verify.Set<AuditEntry>().SingleAsync()).RoleId);
    }

    [Fact]
    public async Task Query_ShouldFilterAndTraverseEqualTimestampWithDeterministicCursor()
    {
        await using SqliteTestDatabase database = await SqliteTestDatabase.CreateAsync();
        UserId target = UserId.New();
        RoleId role = RoleId.New();
        AuditEntry[] entries =
        [
            CreateEntry(new AuditEntryId(new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff")),
                AuditEventType.RoleAssignedToUser, targetUserId: target, roleId: role),
            CreateEntry(new AuditEntryId(new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
                AuditEventType.RoleAssignedToUser, targetUserId: target, roleId: role),
            CreateEntry(new AuditEntryId(new Guid("11111111-1111-1111-1111-111111111111")),
                AuditEventType.LoginSucceeded)
        ];
        await using (ApplicationDbContext seed = database.CreateContext())
        {
            seed.Set<AuditEntry>().AddRange(entries);
            await seed.SaveChangesAsync();
        }

        await using ApplicationDbContext context = database.CreateContext();
        var query = new AuditEntryQuery(context);
        var criteria = new AuditQueryCriteria(
            target, role, null, AuditEventType.RoleAssignedToUser,
            Now.AddMinutes(-1), Now.AddMinutes(1), null, null, 1);
        AuditQuerySlice first = await query.QueryAsync(criteria, CancellationToken.None);
        var cursor = new AuditCursor(
            first.Items[0].OccurredAtUtc,
            new AuditEntryId(first.Items[0].Id));
        AuditQuerySlice second = await query.QueryAsync(
            criteria with { Cursor = cursor }, CancellationToken.None);

        Assert.True(first.HasMore);
        Assert.Single(first.Items);
        Assert.Single(second.Items);
        Assert.NotEqual(first.Items[0].Id, second.Items[0].Id);
        Assert.False(second.HasMore);
    }

    [Fact]
    public async Task Model_ShouldContainQueryIndexesAndNoHistoricalForeignKeys()
    {
        await using SqliteTestDatabase database = await SqliteTestDatabase.CreateAsync();
        await using ApplicationDbContext context = database.CreateContext();
        var entity = context.Model.FindEntityType(typeof(AuditEntry));

        Assert.NotNull(entity);
        Assert.Empty(entity.GetForeignKeys());
        Assert.Contains(entity.GetIndexes(), index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(AuditEntry.OccurredAtUtcTicks), nameof(AuditEntry.SortId)]));
        Assert.Contains(entity.GetIndexes(), index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(AuditEntry.TargetUserId), nameof(AuditEntry.OccurredAtUtcTicks), nameof(AuditEntry.SortId)]));
    }

    private static AuditEntry CreateEntry(
        AuditEntryId id,
        AuditEventType eventType,
        UserId? actorUserId = null,
        UserId? targetUserId = null,
        RoleId? roleId = null,
        UserSessionId? sessionId = null,
        string? permission = null) =>
        AuditEntry.Create(
            id, eventType, AuditOutcome.Success, null, Now, "integration-correlation",
            actorUserId, targetUserId, roleId, sessionId, "127.0.0.1", "Integration Test", permission);
}
