using EnterpriseIdentityService.Application.Abstractions;
using EnterpriseIdentityService.Application.Abstractions.Messaging;
using EnterpriseIdentityService.Application.Abstractions.Persistence;
namespace EnterpriseIdentityService.Application.Authentication.Logout;
public sealed class LogoutCommandHandler(IUserSessionRepository sessions,
    TimeProvider timeProvider) : ICommandHandler<LogoutCommand>
{
    public async Task<Result> Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        await sessions.RevokeAsync(command.SessionId, command.UserId,
            timeProvider.GetUtcNow(), cancellationToken);
        return Result.Success();
    }
}
