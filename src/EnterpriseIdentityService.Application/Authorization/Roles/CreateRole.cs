using EnterpriseIdentityService.Application.Abstractions;
using EnterpriseIdentityService.Application.Abstractions.Messaging;
using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Domain.Roles;
using EnterpriseIdentityService.Domain.Users;
using EnterpriseIdentityService.Application.Auditing;
using EnterpriseIdentityService.Domain.Auditing;

namespace EnterpriseIdentityService.Application.Authorization.Roles;

public sealed record CreateRoleCommand(UserId ActorUserId, string Name) : ICommand<RoleResult>;

public sealed class CreateRoleCommandHandler(
    IRoleRepository roles,
    AuditRecorder audit,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateRoleCommand, RoleResult>
{
    public async Task<Result<RoleResult>> Handle(
        CreateRoleCommand command,
        CancellationToken cancellationToken)
    {
        Role role;
        try
        {
            role = Role.Create(RoleId.New(), command.Name);
        }
        catch (ArgumentException)
        {
            return Result<RoleResult>.Failure(AuthorizationErrors.InvalidRoleName);
        }

        if (await roles.GetByNormalizedNameAsync(role.NormalizedName, cancellationToken) is not null)
        {
            return Result<RoleResult>.Failure(AuthorizationErrors.RoleAlreadyExists);
        }

        roles.Add(role);
        audit.Record(
            AuditEventType.RoleCreated,
            actorUserId: command.ActorUserId,
            roleId: role.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<RoleResult>.Success(role.ToResult());
    }
}
