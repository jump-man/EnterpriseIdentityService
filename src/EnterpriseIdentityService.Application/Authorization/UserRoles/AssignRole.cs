using EnterpriseIdentityService.Application.Abstractions;
using EnterpriseIdentityService.Application.Abstractions.Authorization;
using EnterpriseIdentityService.Application.Abstractions.Messaging;
using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Domain.Roles;
using EnterpriseIdentityService.Domain.Users;
using EnterpriseIdentityService.Application.Auditing;
using EnterpriseIdentityService.Domain.Auditing;

namespace EnterpriseIdentityService.Application.Authorization.UserRoles;

public sealed record AssignRoleCommand(
    UserId ActorUserId,
    UserId UserId,
    RoleId RoleId) : ICommand;

public sealed class AssignRoleCommandHandler(
    IUserRepository users,
    IRoleRepository roles,
    IUserRoleRepository userRoles,
    IAuthorizationSnapshotProvider authorizationSnapshots,
    AuditRecorder audit,
    IUnitOfWork unitOfWork)
    : ICommandHandler<AssignRoleCommand>
{
    public async Task<Result> Handle(
        AssignRoleCommand command,
        CancellationToken cancellationToken)
    {
        User? actor = await users.GetByIdAsync(command.ActorUserId, cancellationToken);
        if (actor is null)
        {
            return Result.Failure(AuthorizationErrors.InvalidActor);
        }

        User? target = await users.GetByIdAsync(command.UserId, cancellationToken);
        if (target is null)
        {
            return Result.Failure(AuthorizationErrors.UserNotFound);
        }

        Role? role = await roles.GetByIdAsync(command.RoleId, cancellationToken);
        if (role is null)
        {
            return Result.Failure(AuthorizationErrors.RoleNotFound);
        }

        if (!role.IsEnabled)
        {
            return Result.Failure(AuthorizationErrors.RoleDisabled);
        }

        AuthorizationSnapshot actorAuthorization =
            await authorizationSnapshots.GetAsync(actor, cancellationToken);
        if (!role.Permissions.Select(item => item.Permission)
                .All(permission => actorAuthorization.Permissions.Contains(
                    permission, StringComparer.Ordinal)))
        {
            audit.Record(
                AuditEventType.AuthorizationChangeRejected,
                AuditOutcome.Failure,
                AuditReasonCode.GrantCeilingViolation,
                actorUserId: command.ActorUserId,
                targetUserId: command.UserId,
                roleId: role.Id);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Failure(AuthorizationErrors.GrantCeilingExceeded);
        }

        if (await userRoles.GetAsync(target.Id, role.Id, cancellationToken) is not null)
        {
            return Result.Failure(AuthorizationErrors.RoleAlreadyAssigned);
        }

        userRoles.Add(UserRole.Create(target.Id, role.Id));
        target.InvalidateAuthorization();
        if (role.Id == BuiltInRoles.AdministratorId)
        {
            role.RecordAssignmentChange();
        }

        audit.Record(
            AuditEventType.RoleAssignedToUser,
            actorUserId: command.ActorUserId,
            targetUserId: command.UserId,
            roleId: role.Id);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyException)
        {
            return Result.Failure(AuthorizationErrors.ConcurrencyConflict);
        }

        return Result.Success();
    }
}
