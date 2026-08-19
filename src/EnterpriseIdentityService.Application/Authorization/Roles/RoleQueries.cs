using EnterpriseIdentityService.Application.Abstractions;
using EnterpriseIdentityService.Application.Abstractions.Messaging;
using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Domain.Roles;

namespace EnterpriseIdentityService.Application.Authorization.Roles;

public sealed record ListRolesQuery : ICommand<IReadOnlyList<RoleResult>>;

public sealed record GetRoleQuery(RoleId RoleId) : ICommand<RoleResult>;

public sealed class ListRolesQueryHandler(IRoleRepository roles)
    : ICommandHandler<ListRolesQuery, IReadOnlyList<RoleResult>>
{
    public async Task<Result<IReadOnlyList<RoleResult>>> Handle(
        ListRolesQuery command,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Role> results = await roles.ListAsync(cancellationToken);
        return Result<IReadOnlyList<RoleResult>>.Success(
            results.Select(role => role.ToResult()).ToArray());
    }
}

public sealed class GetRoleQueryHandler(IRoleRepository roles)
    : ICommandHandler<GetRoleQuery, RoleResult>
{
    public async Task<Result<RoleResult>> Handle(
        GetRoleQuery command,
        CancellationToken cancellationToken)
    {
        Role? role = await roles.GetByIdAsync(command.RoleId, cancellationToken);
        return role is null
            ? Result<RoleResult>.Failure(AuthorizationErrors.RoleNotFound)
            : Result<RoleResult>.Success(role.ToResult());
    }
}
