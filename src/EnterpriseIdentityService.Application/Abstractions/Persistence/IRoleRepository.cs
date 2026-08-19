using EnterpriseIdentityService.Domain.Roles;
using EnterpriseIdentityService.Domain.Users;

namespace EnterpriseIdentityService.Application.Abstractions.Persistence;

public interface IRoleRepository
{
    Task<Role?> GetByIdAsync(RoleId roleId, CancellationToken cancellationToken);

    Task<Role?> GetByNormalizedNameAsync(
        string normalizedName,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Role>> ListAsync(CancellationToken cancellationToken);

    Task<bool> HasAssignedUsersAsync(RoleId roleId, CancellationToken cancellationToken);

    Task<IReadOnlyList<User>> GetAssignedUsersAsync(
        RoleId roleId,
        CancellationToken cancellationToken);

    Task<int> CountViableAdministratorsAsync(CancellationToken cancellationToken);

    void Add(Role role);

    void Remove(Role role);
}
