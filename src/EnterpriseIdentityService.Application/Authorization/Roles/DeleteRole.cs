using EnterpriseIdentityService.Application.Abstractions;
using EnterpriseIdentityService.Application.Abstractions.Messaging;
using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Domain.Roles;
using EnterpriseIdentityService.Domain.Users;
using EnterpriseIdentityService.Application.Auditing;
using EnterpriseIdentityService.Domain.Auditing;

namespace EnterpriseIdentityService.Application.Authorization.Roles;

public sealed record DeleteRoleCommand(UserId ActorUserId, RoleId RoleId) : ICommand;

public sealed class DeleteRoleCommandHandler(
    IRoleRepository roles,
    AuditRecorder audit,
    IUnitOfWork unitOfWork)
    : ICommandHandler<DeleteRoleCommand>
{
    public async Task<Result> Handle(
        DeleteRoleCommand command,
        CancellationToken cancellationToken)
    {
        Role? role = await roles.GetByIdAsync(command.RoleId, cancellationToken);
        if (role is null)
        {
            return Result.Failure(AuthorizationErrors.RoleNotFound);
        }

        if (role.IsSystem)
        {
            audit.Record(
                AuditEventType.AuthorizationChangeRejected,
                AuditOutcome.Failure,
                AuditReasonCode.ProtectedSystemRole,
                actorUserId: command.ActorUserId,
                roleId: role.Id);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Failure(AuthorizationErrors.SystemRoleProtected);
        }

        if (await roles.HasAssignedUsersAsync(role.Id, cancellationToken))
        {
            return Result.Failure(AuthorizationErrors.RoleHasAssignedUsers);
        }

        role.EnsureCanDelete();
        roles.Remove(role);
        audit.Record(
            AuditEventType.RoleDeleted,
            actorUserId: command.ActorUserId,
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
