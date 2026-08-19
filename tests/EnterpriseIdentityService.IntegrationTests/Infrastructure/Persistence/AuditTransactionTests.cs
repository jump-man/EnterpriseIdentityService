using EnterpriseIdentityService.Application.Abstractions.Auditing;
using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Application.Auditing;
using EnterpriseIdentityService.Application.Authorization;
using EnterpriseIdentityService.Application.Authorization.Roles;
using EnterpriseIdentityService.Domain.Auditing;
using EnterpriseIdentityService.Domain.Roles;
using EnterpriseIdentityService.Domain.Users;
using EnterpriseIdentityService.Infrastructure.Persistence;
using EnterpriseIdentityService.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseIdentityService.IntegrationTests.Infrastructure.Persistence;

public sealed class AuditTransactionTests
{
    [Fact]
    public async Task AuditInsertFailure_ShouldRollBackCriticalRoleMutation()
    {
        await using SqliteTestDatabase database = await SqliteTestDatabase.CreateAsync();
        await using ApplicationDbContext context = database.CreateContext();
        await context.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER RejectAudit BEFORE INSERT ON AuditEntries " +
            "BEGIN SELECT RAISE(ABORT, 'audit unavailable'); END;");
        var handler = new CreateRoleCommandHandler(
            new RoleRepository(context),
            Recorder(context),
            context);

        await Assert.ThrowsAsync<DbUpdateException>(() => handler.Handle(
            new CreateRoleCommand(UserId.New(), "Must Roll Back"),
            CancellationToken.None));

        await using ApplicationDbContext verification = database.CreateContext();
        Assert.False(await verification.Set<Role>()
            .AnyAsync(role => role.NormalizedName == "MUST ROLL BACK"));
        Assert.Empty(await verification.Set<AuditEntry>().ToListAsync());
    }

    [Fact]
    public async Task ConcurrencyFailure_ShouldNotLeaveMisleadingSuccessAudit()
    {
        await using SqliteTestDatabase database = await SqliteTestDatabase.CreateAsync();
        Role role = Role.Create(RoleId.New(), "Original");
        await using (ApplicationDbContext seed = database.CreateContext())
        {
            seed.Set<Role>().Add(role);
            await seed.SaveChangesAsync();
        }

        await using ApplicationDbContext firstContext = database.CreateContext();
        await using ApplicationDbContext secondContext = database.CreateContext();
        var firstRoles = new RoleRepository(firstContext);
        var secondRoles = new RoleRepository(secondContext);
        _ = await secondRoles.GetByIdAsync(role.Id, CancellationToken.None);
        var first = new RenameRoleCommandHandler(
            firstRoles, Recorder(firstContext), firstContext);
        var second = new RenameRoleCommandHandler(
            secondRoles, Recorder(secondContext), secondContext);
        UserId actor = UserId.New();

        var firstResult = await first.Handle(
            new RenameRoleCommand(actor, role.Id, "First"), CancellationToken.None);
        var secondResult = await second.Handle(
            new RenameRoleCommand(actor, role.Id, "Second"), CancellationToken.None);

        Assert.True(firstResult.IsSuccess);
        Assert.Equal(AuthorizationErrors.ConcurrencyConflict, secondResult.Error);
        await using ApplicationDbContext verification = database.CreateContext();
        Assert.Equal("First", (await verification.Set<Role>()
            .SingleAsync(item => item.Id == role.Id)).Name);
        AuditEntry audit = Assert.Single(await verification.Set<AuditEntry>().ToListAsync());
        Assert.Equal(AuditEventType.RoleRenamed, audit.EventType);
    }

    private static AuditRecorder Recorder(ApplicationDbContext context) => new(
        new AuditEntryRepository(context),
        new FixedAuditContextProvider(),
        TimeProvider.System);

    private sealed class FixedAuditContextProvider : IAuditContextProvider
    {
        public AuditContext GetCurrent() =>
            new("transaction-test", "127.0.0.1", "Integration Test");
    }
}
