using EnterpriseIdentityService.Application.Abstractions;
using EnterpriseIdentityService.Application.Abstractions.Authorization;
using EnterpriseIdentityService.Application.Abstractions.Messaging;
using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Domain.Roles;
using EnterpriseIdentityService.Domain.Users;

namespace EnterpriseIdentityService.Application.Authorization.Roles;

public sealed record ReplaceRolePermissionsCommand(
    UserId ActorUserId,
    RoleId RoleId,
    IReadOnlyCollection<string> Permissions) : ICommand<RoleResult>;

public sealed class ReplaceRolePermissionsCommandHandler(
    IRoleRepository roles,
    IUserRepository users,
    IAuthorizationSnapshotProvider authorizationSnapshots,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ReplaceRolePermissionsCommand, RoleResult>
{
    public async Task<Result<RoleResult>> Handle(
        ReplaceRolePermissionsCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Permissions is null ||
            command.Permissions.Any(permission =>
                !EnterpriseIdentityService.Application.Authorization.Permissions.Contains(permission)))
        {
            return Result<RoleResult>.Failure(AuthorizationErrors.UnknownPermission);
        }

        string[] requested = command.Permissions.Distinct(StringComparer.Ordinal).ToArray();
        Role? role = await roles.GetByIdAsync(command.RoleId, cancellationToken);
        if (role is null)
        {
            return Result<RoleResult>.Failure(AuthorizationErrors.RoleNotFound);
        }

        if (role.IsSystem)
        {
            return Result<RoleResult>.Failure(AuthorizationErrors.SystemRoleProtected);
        }

        User? actor = await users.GetByIdAsync(command.ActorUserId, cancellationToken);
        if (actor is null)
        {
            return Result<RoleResult>.Failure(AuthorizationErrors.InvalidActor);
        }

        AuthorizationSnapshot actorAuthorization =
            await authorizationSnapshots.GetAsync(actor, cancellationToken);
        if (!requested.All(permission =>
                actorAuthorization.Permissions.Contains(permission, StringComparer.Ordinal)))
        {
            return Result<RoleResult>.Failure(AuthorizationErrors.GrantCeilingExceeded);
        }

        string[] current = role.Permissions.Select(item => item.Permission).ToArray();
        if (current.ToHashSet(StringComparer.Ordinal).SetEquals(requested))
        {
            return Result<RoleResult>.Success(role.ToResult());
        }

        IReadOnlyList<User> affectedUsers =
            await roles.GetAssignedUsersAsync(role.Id, cancellationToken);
        role.ReplacePermissions(requested);
        foreach (User affectedUser in affectedUsers)
        {
            affectedUser.InvalidateAuthorization();
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyException)
        {
            return Result<RoleResult>.Failure(AuthorizationErrors.ConcurrencyConflict);
        }

        return Result<RoleResult>.Success(role.ToResult());
    }
}
