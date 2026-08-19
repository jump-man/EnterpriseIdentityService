using EnterpriseIdentityService.Domain.Roles;

namespace EnterpriseIdentityService.Application.Authorization;

internal static class RoleMappings
{
    public static RoleResult ToResult(this Role role) => new(
        role.Id.Value,
        role.Name,
        role.IsSystem,
        role.IsEnabled,
        role.Permissions
            .Select(rolePermission => rolePermission.Permission)
            .Order(StringComparer.Ordinal)
            .ToArray());
}
