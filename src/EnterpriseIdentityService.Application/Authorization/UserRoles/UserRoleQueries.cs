using EnterpriseIdentityService.Application.Abstractions;
using EnterpriseIdentityService.Application.Abstractions.Messaging;
using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Domain.Roles;
using EnterpriseIdentityService.Domain.Users;

namespace EnterpriseIdentityService.Application.Authorization.UserRoles;

public sealed record ListUserRolesQuery(UserId UserId) : ICommand<IReadOnlyList<RoleResult>>;

public sealed class ListUserRolesQueryHandler(
    IUserRepository users,
    IUserRoleRepository userRoles)
    : ICommandHandler<ListUserRolesQuery, IReadOnlyList<RoleResult>>
{
    public async Task<Result<IReadOnlyList<RoleResult>>> Handle(
        ListUserRolesQuery command,
        CancellationToken cancellationToken)
    {
        if (await users.GetByIdAsync(command.UserId, cancellationToken) is null)
        {
            return Result<IReadOnlyList<RoleResult>>.Failure(AuthorizationErrors.UserNotFound);
        }

        IReadOnlyList<Role> roles =
            await userRoles.GetRolesAsync(command.UserId, cancellationToken);
        return Result<IReadOnlyList<RoleResult>>.Success(
            roles.Select(role => role.ToResult()).ToArray());
    }
}
