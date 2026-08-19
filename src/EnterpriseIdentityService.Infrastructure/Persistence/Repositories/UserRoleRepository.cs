using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Domain.Roles;
using EnterpriseIdentityService.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseIdentityService.Infrastructure.Persistence.Repositories;

internal sealed class UserRoleRepository(ApplicationDbContext dbContext) : IUserRoleRepository
{
    public Task<UserRole?> GetAsync(
        UserId userId,
        RoleId roleId,
        CancellationToken cancellationToken) =>
        dbContext.UserRoles.SingleOrDefaultAsync(
            userRole => userRole.UserId == userId && userRole.RoleId == roleId,
            cancellationToken);

    public async Task<IReadOnlyList<Role>> GetRolesAsync(
        UserId userId,
        CancellationToken cancellationToken) =>
        await (from userRole in dbContext.UserRoles
               join role in dbContext.Roles on userRole.RoleId equals role.Id
               where userRole.UserId == userId
               orderby role.Name
               select role)
            .Include(role => role.Permissions)
            .ToListAsync(cancellationToken);

    public void Add(UserRole userRole) => dbContext.UserRoles.Add(userRole);

    public void Remove(UserRole userRole) => dbContext.UserRoles.Remove(userRole);
}
