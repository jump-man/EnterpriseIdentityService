using EnterpriseIdentityService.Application.Authorization;

namespace EnterpriseIdentityService.UnitTests.Authorization;

public sealed class PermissionCatalogTests
{
    [Fact]
    public void All_ShouldContainOnlyThePhase12Capabilities()
    {
        Assert.Equal(
            ["roles.read", "roles.manage", "users.roles.read", "users.roles.manage"],
            Permissions.All);
        Assert.Equal(Permissions.All.Count, Permissions.All.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData("roles.read", true)]
    [InlineData("roles.manage", true)]
    [InlineData("whatever.admin", false)]
    [InlineData("ROLES.READ", false)]
    public void Contains_ShouldUseStableOrdinalIdentifiers(string permission, bool expected)
    {
        Assert.Equal(expected, Permissions.Contains(permission));
    }
}
