namespace EnterpriseIdentityService.Domain.Roles;

public sealed class RolePermission
{
    private RolePermission(RoleId roleId, string permission)
    {
        RoleId = roleId;
        Permission = permission;
    }

    public RoleId RoleId { get; }

    public string Permission { get; }

    internal static RolePermission Create(RoleId roleId, string permission)
    {
        if (string.IsNullOrWhiteSpace(permission))
        {
            throw new ArgumentException("A permission identifier is required.", nameof(permission));
        }

        return new RolePermission(roleId, permission.Trim());
    }
}
