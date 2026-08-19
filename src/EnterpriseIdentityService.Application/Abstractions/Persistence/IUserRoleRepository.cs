using EnterpriseIdentityService.Domain.Roles;
using EnterpriseIdentityService.Domain.Users;

namespace EnterpriseIdentityService.Application.Abstractions.Persistence;

public interface IUserRoleRepository
{
    Task<UserRole?> GetAsync(
        UserId userId,
        RoleId roleId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Role>> GetRolesAsync(
        UserId userId,
        CancellationToken cancellationToken);

    void Add(UserRole userRole);

    void Remove(UserRole userRole);
}
