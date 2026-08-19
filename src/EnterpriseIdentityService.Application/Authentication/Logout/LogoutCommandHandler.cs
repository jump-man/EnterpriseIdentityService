using EnterpriseIdentityService.Application.Abstractions;
using EnterpriseIdentityService.Application.Abstractions.Messaging;
using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Application.Auditing;
using EnterpriseIdentityService.Domain.Auditing;
namespace EnterpriseIdentityService.Application.Authentication.Logout;
public sealed class LogoutCommandHandler(IUserSessionRepository sessions,
    IUnitOfWork unitOfWork, AuditRecorder audit, TimeProvider timeProvider) : ICommandHandler<LogoutCommand>
{
    public async Task<Result> Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        var session = await sessions.GetByIdAsync(command.SessionId, cancellationToken);
        if (session is null || session.UserId != command.UserId)
        {
            return Result.Success();
        }

        session.Revoke(timeProvider.GetUtcNow());
        audit.Record(
            AuditEventType.Logout,
            actorUserId: command.UserId,
            targetUserId: command.UserId,
            sessionId: command.SessionId);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyException)
        {
            return Result.Failure(LogoutErrors.Conflict);
        }

        return Result.Success();
    }
}
