using EnterpriseIdentityService.Application.Abstractions;
using EnterpriseIdentityService.Application.Abstractions.Authorization;
using EnterpriseIdentityService.Application.Abstractions.Messaging;
using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Domain.Roles;
using EnterpriseIdentityService.Domain.Users;
using EnterpriseIdentityService.Application.Auditing;
using EnterpriseIdentityService.Domain.Auditing;

namespace EnterpriseIdentityService.Application.Authorization.UserRoles;

public sealed record RemoveRoleCommand(
    UserId ActorUserId,
    UserId UserId,
    RoleId RoleId) : ICommand;

public sealed class RemoveRoleCommandHandler(
    IUserRepository users,
    IRoleRepository roles,
    IUserRoleRepository userRoles,
    IAuthorizationSnapshotProvider authorizationSnapshots,
    AuditRecorder audit,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveRoleCommand>
{
    public async Task<Result> Handle(
        RemoveRoleCommand command,
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

        UserRole? assignment = await userRoles.GetAsync(target.Id, role.Id, cancellationToken);
        if (assignment is null)
        {
            return Result.Failure(AuthorizationErrors.RoleNotAssigned);
        }

        if (role.Id == BuiltInRoles.AdministratorId &&
            target.Status == UserStatus.Active &&
            await roles.CountViableAdministratorsAsync(cancellationToken) <= 1)
        {
            audit.Record(
                AuditEventType.AuthorizationChangeRejected,
                AuditOutcome.Failure,
                AuditReasonCode.LastAdministratorProtection,
                actorUserId: command.ActorUserId,
                targetUserId: command.UserId,
                roleId: role.Id);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Failure(AuthorizationErrors.LastAdministratorRequired);
        }

        userRoles.Remove(assignment);
        target.InvalidateAuthorization();
        if (role.Id == BuiltInRoles.AdministratorId)
        {
            role.RecordAssignmentChange();
        }

        audit.Record(
            AuditEventType.RoleRemovedFromUser,
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
