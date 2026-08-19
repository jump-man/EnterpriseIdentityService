using EnterpriseIdentityService.Application.Abstractions;
using EnterpriseIdentityService.Application.Abstractions.Authorization;
using EnterpriseIdentityService.Application.Abstractions.Messaging;
using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Domain.Roles;
using EnterpriseIdentityService.Domain.Users;

namespace EnterpriseIdentityService.Application.Authorization.Roles;

public sealed record SetRoleEnabledCommand(
    UserId ActorUserId,
    RoleId RoleId,
    bool IsEnabled) : ICommand<RoleResult>;

public sealed class SetRoleEnabledCommandHandler(
    IRoleRepository roles,
    IUserRepository users,
    IAuthorizationSnapshotProvider authorizationSnapshots,
    IUnitOfWork unitOfWork)
    : ICommandHandler<SetRoleEnabledCommand, RoleResult>
{
    public async Task<Result<RoleResult>> Handle(
        SetRoleEnabledCommand command,
        CancellationToken cancellationToken)
    {
        Role? role = await roles.GetByIdAsync(command.RoleId, cancellationToken);
        if (role is null)
        {
            return Result<RoleResult>.Failure(AuthorizationErrors.RoleNotFound);
        }

        if (role.IsSystem)
        {
            return Result<RoleResult>.Failure(AuthorizationErrors.SystemRoleProtected);
        }

        if (role.IsEnabled == command.IsEnabled)
        {
            return Result<RoleResult>.Success(role.ToResult());
        }

        User? actor = await users.GetByIdAsync(command.ActorUserId, cancellationToken);
        if (actor is null)
        {
            return Result<RoleResult>.Failure(AuthorizationErrors.InvalidActor);
        }

        AuthorizationSnapshot actorAuthorization =
            await authorizationSnapshots.GetAsync(actor, cancellationToken);
        if (!role.Permissions.Select(item => item.Permission)
                .All(permission => actorAuthorization.Permissions.Contains(
                    permission, StringComparer.Ordinal)))
        {
            return Result<RoleResult>.Failure(AuthorizationErrors.GrantCeilingExceeded);
        }

        IReadOnlyList<User> affectedUsers =
            await roles.GetAssignedUsersAsync(role.Id, cancellationToken);
        if (command.IsEnabled)
        {
            role.Enable();
        }
        else
        {
            role.Disable();
        }

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
