using EnterpriseIdentityService.Application.Abstractions;
using EnterpriseIdentityService.Application.Abstractions.Messaging;
using EnterpriseIdentityService.Application.Abstractions.Persistence;
namespace EnterpriseIdentityService.Application.Authentication.LogoutAll;
public sealed class LogoutAllCommandHandler(IUserRepository users, IUserSessionRepository sessions,
    IUnitOfWork unitOfWork, TimeProvider timeProvider) : ICommandHandler<LogoutAllCommand>
{
    public async Task<Result> Handle(LogoutAllCommand command, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null) return Result.Failure(LogoutAllErrors.InvalidAuthentication);
        DateTimeOffset now = timeProvider.GetUtcNow();
        foreach (var session in await sessions.GetActiveByUserIdAsync(user.Id, cancellationToken)) session.Revoke(now);
        user.InvalidateAuthentication();
        try { await unitOfWork.SaveChangesAsync(cancellationToken); }
        catch (ConcurrencyException) { return Result.Failure(LogoutAllErrors.Conflict); }
        return Result.Success();
    }
}
