using EnterpriseIdentityService.Application.Abstractions;
using EnterpriseIdentityService.Application.Abstractions.Messaging;
using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Domain.Roles;
using EnterpriseIdentityService.Domain.Users;
using EnterpriseIdentityService.Application.Auditing;
using EnterpriseIdentityService.Domain.Auditing;

namespace EnterpriseIdentityService.Application.Authorization.Roles;

public sealed record RenameRoleCommand(UserId ActorUserId, RoleId RoleId, string Name) : ICommand<RoleResult>;

public sealed class RenameRoleCommandHandler(
    IRoleRepository roles,
    AuditRecorder audit,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RenameRoleCommand, RoleResult>
{
    public async Task<Result<RoleResult>> Handle(
        RenameRoleCommand command,
        CancellationToken cancellationToken)
    {
        Role? role = await roles.GetByIdAsync(command.RoleId, cancellationToken);
        if (role is null)
        {
            return Result<RoleResult>.Failure(AuthorizationErrors.RoleNotFound);
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
            return Result<RoleResult>.Failure(AuthorizationErrors.SystemRoleProtected);
        }

        string normalizedName;
        try
        {
            normalizedName = Role.NormalizeName(command.Name);
        }
        catch (ArgumentException)
        {
            return Result<RoleResult>.Failure(AuthorizationErrors.InvalidRoleName);
        }

        Role? existing = await roles.GetByNormalizedNameAsync(normalizedName, cancellationToken);
        if (existing is not null && existing.Id != role.Id)
        {
            return Result<RoleResult>.Failure(AuthorizationErrors.RoleAlreadyExists);
        }

        role.Rename(command.Name);
        audit.Record(
            AuditEventType.RoleRenamed,
            actorUserId: command.ActorUserId,
            roleId: role.Id);
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
