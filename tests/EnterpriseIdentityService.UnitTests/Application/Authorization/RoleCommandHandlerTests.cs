using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Application.Authorization;
using EnterpriseIdentityService.Application.Authorization.Roles;
using EnterpriseIdentityService.Domain.Roles;
using EnterpriseIdentityService.Domain.Users;

namespace EnterpriseIdentityService.UnitTests.Application.Authorization;

public sealed class RoleCommandHandlerTests
{
    [Fact]
    public async Task Create_ShouldPersistNormalizedEnabledRole()
    {
        var roles = new FakeRoleRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CreateRoleCommandHandler(roles, unitOfWork);

        var result = await handler.Handle(
            new CreateRoleCommand(" Support "), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Role role = Assert.Single(roles.Items);
        Assert.Equal("Support", role.Name);
        Assert.Equal("SUPPORT", role.NormalizedName);
        Assert.True(role.IsEnabled);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Create_ShouldRejectDuplicateNormalizedNameWithoutSaving()
    {
        var roles = new FakeRoleRepository();
        roles.Items.Add(Role.Create(RoleId.New(), "Support"));
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CreateRoleCommandHandler(roles, unitOfWork);

        var result = await handler.Handle(
            new CreateRoleCommand(" support "), CancellationToken.None);

        Assert.Equal(AuthorizationErrors.RoleAlreadyExists, result.Error);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ReplacePermissions_ShouldRejectUnknownCatalogEntryBeforePersistence()
    {
        var roles = new FakeRoleRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ReplaceRolePermissionsCommandHandler(
            roles, null!, null!, unitOfWork);

        var result = await handler.Handle(
            new ReplaceRolePermissionsCommand(
                UserId.New(), RoleId.New(), ["whatever.admin"]),
            CancellationToken.None);

        Assert.Equal(AuthorizationErrors.UnknownPermission, result.Error);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }

    private sealed class FakeRoleRepository : IRoleRepository
    {
        public List<Role> Items { get; } = [];

        public Task<Role?> GetByIdAsync(RoleId roleId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(role => role.Id == roleId));

        public Task<Role?> GetByNormalizedNameAsync(
            string normalizedName,
            CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(role => role.NormalizedName == normalizedName));

        public Task<IReadOnlyList<Role>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Role>>(Items);

        public Task<bool> HasAssignedUsersAsync(RoleId roleId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<IReadOnlyList<User>> GetAssignedUsersAsync(
            RoleId roleId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<User>>([]);

        public Task<int> CountViableAdministratorsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(1);

        public void Add(Role role) => Items.Add(role);

        public void Remove(Role role) => Items.Remove(role);
    }
}
