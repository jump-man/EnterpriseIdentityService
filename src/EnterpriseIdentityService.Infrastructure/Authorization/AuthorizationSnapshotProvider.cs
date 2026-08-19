using EnterpriseIdentityService.Application.Abstractions.Authorization;
using EnterpriseIdentityService.Application.Authorization;
using EnterpriseIdentityService.Domain.Users;
using EnterpriseIdentityService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseIdentityService.Infrastructure.Authorization;

internal sealed class AuthorizationSnapshotProvider(ApplicationDbContext dbContext)
    : IAuthorizationSnapshotProvider
{
    public async Task<AuthorizationSnapshot> GetAsync(
        User user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        string[] permissions = await (
            from userRole in dbContext.UserRoles
            join role in dbContext.Roles on userRole.RoleId equals role.Id
            join rolePermission in dbContext.RolePermissions on role.Id equals rolePermission.RoleId
            where userRole.UserId == user.Id && role.IsEnabled
            select rolePermission.Permission)
            .Distinct()
            .OrderBy(permission => permission)
            .ToArrayAsync(cancellationToken);

        return new AuthorizationSnapshot(user.AuthorizationVersion, permissions);
    }
}
