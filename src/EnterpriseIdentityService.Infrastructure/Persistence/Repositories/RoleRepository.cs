using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Domain.Roles;
using EnterpriseIdentityService.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseIdentityService.Infrastructure.Persistence.Repositories;

internal sealed class RoleRepository(ApplicationDbContext dbContext) : IRoleRepository
{
    public Task<Role?> GetByIdAsync(RoleId roleId, CancellationToken cancellationToken) =>
        dbContext.Roles
            .Include(role => role.Permissions)
            .SingleOrDefaultAsync(role => role.Id == roleId, cancellationToken);

    public Task<Role?> GetByNormalizedNameAsync(
        string normalizedName,
        CancellationToken cancellationToken) =>
        dbContext.Roles
            .Include(role => role.Permissions)
            .SingleOrDefaultAsync(
                role => role.NormalizedName == normalizedName,
                cancellationToken);

    public async Task<IReadOnlyList<Role>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.Roles
            .Include(role => role.Permissions)
            .OrderBy(role => role.Name)
            .ToListAsync(cancellationToken);

    public Task<bool> HasAssignedUsersAsync(
        RoleId roleId,
        CancellationToken cancellationToken) =>
        dbContext.UserRoles.AnyAsync(userRole => userRole.RoleId == roleId, cancellationToken);

    public async Task<IReadOnlyList<User>> GetAssignedUsersAsync(
        RoleId roleId,
        CancellationToken cancellationToken) =>
        await (
            from userRole in dbContext.UserRoles
            join user in dbContext.Users on userRole.UserId equals user.Id
            where userRole.RoleId == roleId
            select user)
            .ToListAsync(cancellationToken);

    public Task<int> CountViableAdministratorsAsync(CancellationToken cancellationToken) =>
        (from userRole in dbContext.UserRoles
         join user in dbContext.Users on userRole.UserId equals user.Id
         where userRole.RoleId == BuiltInRoles.AdministratorId && user.Status == UserStatus.Active
         select userRole.UserId)
        .Distinct()
        .CountAsync(cancellationToken);

    public void Add(Role role) => dbContext.Roles.Add(role);

    public void Remove(Role role) => dbContext.Roles.Remove(role);
}
