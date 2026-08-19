using EnterpriseIdentityService.Domain.Users;

namespace EnterpriseIdentityService.Domain.Roles;

public sealed class UserRole
{
    private UserRole(UserId userId, RoleId roleId)
    {
        UserId = userId;
        RoleId = roleId;
    }

    public UserId UserId { get; }

    public RoleId RoleId { get; }

    public static UserRole Create(UserId userId, RoleId roleId) => new(userId, roleId);
}
