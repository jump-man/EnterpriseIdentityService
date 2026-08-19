using EnterpriseIdentityService.Application.Authorization;
using EnterpriseIdentityService.Domain.Roles;
using EnterpriseIdentityService.Domain.Users;
using EnterpriseIdentityService.Infrastructure.Authorization;
using EnterpriseIdentityService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseIdentityService.IntegrationTests.Infrastructure.Persistence;

public sealed class AuthorizationPersistenceTests
{
    [Fact]
    public async Task Model_ShouldSeedProtectedAdministratorWithCompleteCatalog()
    {
        await using SqliteTestDatabase database = await SqliteTestDatabase.CreateAsync();
        await using ApplicationDbContext context = database.CreateContext();

        Role administrator = await context.Set<Role>()
            .Include(role => role.Permissions)
            .SingleAsync(role => role.Id == BuiltInRoles.AdministratorId);

        Assert.True(administrator.IsSystem);
        Assert.True(administrator.IsEnabled);
        Assert.Equal(BuiltInRoles.AdministratorName, administrator.Name);
        Assert.True(Permissions.All.ToHashSet(StringComparer.Ordinal).SetEquals(
            administrator.Permissions.Select(item => item.Permission)));
    }

    [Fact]
    public async Task SaveChanges_ShouldPersistRolePermissionsAndUserAssignment()
    {
        await using SqliteTestDatabase database = await SqliteTestDatabase.CreateAsync();
        User user = CreateActiveUser("user@example.com", "user.one");
        Role role = Role.Create(RoleId.New(), "Support");
        role.ReplacePermissions([Permissions.Roles.Read]);

        await using (ApplicationDbContext write = database.CreateContext())
        {
            write.Set<User>().Add(user);
            write.Set<Role>().Add(role);
            write.Set<UserRole>().Add(UserRole.Create(user.Id, role.Id));
            await write.SaveChangesAsync();
        }

        await using ApplicationDbContext read = database.CreateContext();
        Role persisted = await read.Set<Role>()
            .Include(item => item.Permissions)
            .SingleAsync(item => item.Id == role.Id);
        UserRole assignment = await read.Set<UserRole>().SingleAsync();

        Assert.Equal("SUPPORT", persisted.NormalizedName);
        Assert.Equal(Permissions.Roles.Read, Assert.Single(persisted.Permissions).Permission);
        Assert.Equal(user.Id, assignment.UserId);
        Assert.Equal(role.Id, assignment.RoleId);
    }

    [Fact]
    public async Task SaveChanges_ShouldRejectDuplicateNormalizedName()
    {
        await using SqliteTestDatabase database = await SqliteTestDatabase.CreateAsync();
        User user = CreateActiveUser("user@example.com", "user.one");
        Role first = Role.Create(RoleId.New(), "Support");
        Role second = Role.Create(RoleId.New(), " support ");

        await using ApplicationDbContext context = database.CreateContext();
        context.Set<User>().Add(user);
        context.Set<Role>().AddRange(first, second);
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

    }

    [Fact]
    public async Task SaveChanges_ShouldRejectDuplicateUserRoleAssignment()
    {
        await using SqliteTestDatabase database = await SqliteTestDatabase.CreateAsync();
        User user = CreateActiveUser("user@example.com", "user.one");
        Role role = Role.Create(RoleId.New(), "Support");
        await using (ApplicationDbContext seed = database.CreateContext())
        {
            seed.Set<User>().Add(user);
            seed.Set<Role>().Add(role);
            seed.Set<UserRole>().Add(UserRole.Create(user.Id, role.Id));
            await seed.SaveChangesAsync();
        }

        await using ApplicationDbContext context = database.CreateContext();
        context.Set<UserRole>().Add(UserRole.Create(user.Id, role.Id));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task AuthorizationSnapshot_ShouldUnionEnabledRolesAndIgnoreDisabledRoles()
    {
        await using SqliteTestDatabase database = await SqliteTestDatabase.CreateAsync();
        User user = CreateActiveUser("user@example.com", "user.one");
        Role enabled = Role.Create(RoleId.New(), "Enabled");
        enabled.ReplacePermissions([Permissions.Roles.Read, Permissions.UserRoles.Read]);
        Role disabled = Role.Create(RoleId.New(), "Disabled");
        disabled.ReplacePermissions([Permissions.Roles.Read, Permissions.Roles.Manage]);
        disabled.Disable();

        await using ApplicationDbContext context = database.CreateContext();
        context.Set<User>().Add(user);
        context.Set<Role>().AddRange(enabled, disabled);
        context.Set<UserRole>().AddRange(
            UserRole.Create(user.Id, enabled.Id),
            UserRole.Create(user.Id, disabled.Id));
        await context.SaveChangesAsync();

        var provider = new AuthorizationSnapshotProvider(context);
        AuthorizationSnapshot snapshot = await provider.GetAsync(user, CancellationToken.None);

        Assert.Equal(user.AuthorizationVersion, snapshot.AuthorizationVersion);
        Assert.Equal(
            [Permissions.Roles.Read, Permissions.UserRoles.Read],
            snapshot.Permissions);
    }

    [Fact]
    public async Task ConcurrentRoleAndAuthorizationVersionChanges_ShouldConflict()
    {
        await using SqliteTestDatabase database = await SqliteTestDatabase.CreateAsync();
        User user = CreateActiveUser("user@example.com", "user.one");
        Role role = Role.Create(RoleId.New(), "Support");
        await using (ApplicationDbContext seed = database.CreateContext())
        {
            seed.Set<User>().Add(user);
            seed.Set<Role>().Add(role);
            await seed.SaveChangesAsync();
        }

        await using ApplicationDbContext first = database.CreateContext();
        await using ApplicationDbContext second = database.CreateContext();
        Role firstRole = await first.Set<Role>().SingleAsync(item => item.Id == role.Id);
        Role secondRole = await second.Set<Role>().SingleAsync(item => item.Id == role.Id);
        User firstUser = await first.Set<User>().SingleAsync(item => item.Id == user.Id);
        User secondUser = await second.Set<User>().SingleAsync(item => item.Id == user.Id);

        firstRole.Rename("First");
        firstUser.InvalidateAuthorization();
        await first.SaveChangesAsync();

        secondRole.Rename("Second");
        secondUser.InvalidateAuthorization();
        await Assert.ThrowsAsync<EnterpriseIdentityService.Application.Abstractions.Persistence.ConcurrencyException>(
            () => second.SaveChangesAsync());
    }

    [Fact]
    public async Task ConcurrentAdministratorRemovals_ShouldLeaveOneViableAdministrator()
    {
        await using SqliteTestDatabase database = await SqliteTestDatabase.CreateAsync();
        User firstAdmin = CreateActiveUser("first@example.com", "first.admin");
        User secondAdmin = CreateActiveUser("second@example.com", "second.admin");
        await using (ApplicationDbContext seed = database.CreateContext())
        {
            seed.Set<User>().AddRange(firstAdmin, secondAdmin);
            seed.Set<UserRole>().AddRange(
                UserRole.Create(firstAdmin.Id, BuiltInRoles.AdministratorId),
                UserRole.Create(secondAdmin.Id, BuiltInRoles.AdministratorId));
            await seed.SaveChangesAsync();
        }

        await using ApplicationDbContext first = database.CreateContext();
        await using ApplicationDbContext second = database.CreateContext();
        Role firstGuard = await first.Set<Role>()
            .SingleAsync(role => role.Id == BuiltInRoles.AdministratorId);
        Role secondGuard = await second.Set<Role>()
            .SingleAsync(role => role.Id == BuiltInRoles.AdministratorId);
        UserRole firstAssignment = await first.Set<UserRole>()
            .SingleAsync(item => item.UserId == firstAdmin.Id);
        UserRole secondAssignment = await second.Set<UserRole>()
            .SingleAsync(item => item.UserId == secondAdmin.Id);
        User firstUser = await first.Set<User>().SingleAsync(item => item.Id == firstAdmin.Id);
        User secondUser = await second.Set<User>().SingleAsync(item => item.Id == secondAdmin.Id);

        first.Set<UserRole>().Remove(firstAssignment);
        firstUser.InvalidateAuthorization();
        firstGuard.RecordAssignmentChange();
        second.Set<UserRole>().Remove(secondAssignment);
        secondUser.InvalidateAuthorization();
        secondGuard.RecordAssignmentChange();

        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<EnterpriseIdentityService.Application.Abstractions.Persistence.ConcurrencyException>(
            () => second.SaveChangesAsync());

        await using ApplicationDbContext verification = database.CreateContext();
        Assert.Equal(1, await verification.Set<UserRole>()
            .CountAsync(item => item.RoleId == BuiltInRoles.AdministratorId));
    }

    private static User CreateActiveUser(string email, string username)
    {
        User user = User.Register(
            UserId.New(), Email.Create(email), Username.Create(username),
            PasswordHash.Create("HASHED-PASSWORD"), DateTimeOffset.UtcNow);
        user.VerifyEmail(DateTimeOffset.UtcNow);
        return user;
    }
}
