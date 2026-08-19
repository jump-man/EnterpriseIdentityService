using EnterpriseIdentityService.Application.Authorization;
using EnterpriseIdentityService.Domain.Roles;

namespace EnterpriseIdentityService.UnitTests.Roles;

public sealed class RoleTests
{
    [Fact]
    public void Create_ShouldNormalizeNameAndStartEnabled()
    {
        Role role = Role.Create(RoleId.New(), "  Support Operators  ");

        Assert.Equal("Support Operators", role.Name);
        Assert.Equal("SUPPORT OPERATORS", role.NormalizedName);
        Assert.True(role.IsEnabled);
        Assert.False(role.IsSystem);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ShouldRejectInvalidName(string name)
    {
        Assert.Throws<ArgumentException>(() => Role.Create(RoleId.New(), name));
    }

    [Fact]
    public void Rename_ShouldChangeNameAndConcurrencyVersion()
    {
        Role role = Role.Create(RoleId.New(), "Support");

        role.Rename("Auditors");

        Assert.Equal("Auditors", role.Name);
        Assert.Equal("AUDITORS", role.NormalizedName);
        Assert.Equal(1, role.Version);
    }

    [Fact]
    public void DisableAndEnable_ShouldChangeStateAndVersion()
    {
        Role role = Role.Create(RoleId.New(), "Support");

        role.Disable();
        role.Enable();

        Assert.True(role.IsEnabled);
        Assert.Equal(2, role.Version);
    }

    [Fact]
    public void ReplacePermissions_ShouldBeDistinctAndIdempotent()
    {
        Role role = Role.Create(RoleId.New(), "Support");

        role.ReplacePermissions([Permissions.Roles.Read, Permissions.Roles.Read]);
        role.ReplacePermissions([Permissions.Roles.Read]);

        Assert.Equal(Permissions.Roles.Read, Assert.Single(role.Permissions).Permission);
        Assert.Equal(1, role.Version);
    }

    [Fact]
    public void SystemRole_ShouldRejectRenameDisableDeleteAndPermissionMutation()
    {
        Role role = Role.CreateSystem(
            BuiltInRoles.AdministratorId,
            BuiltInRoles.AdministratorName,
            Permissions.All);

        Assert.Throws<InvalidOperationException>(() => role.Rename("Root"));
        Assert.Throws<InvalidOperationException>(() => role.Disable());
        Assert.Throws<InvalidOperationException>(() => role.EnsureCanDelete());
        Assert.Throws<InvalidOperationException>(() => role.ReplacePermissions([]));
        Assert.True(role.IsEnabled);
        Assert.Equal(Permissions.All.Count, role.Permissions.Count);
    }
}
