using EnterpriseIdentityService.Application.Abstractions;
using EnterpriseIdentityService.Application.Abstractions.Authentication;
using EnterpriseIdentityService.Application.Abstractions.Messaging;
using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Domain.Users;

namespace EnterpriseIdentityService.Application.Users.ResetPassword;

public sealed class ResetPasswordCommandHandler(
    IPasswordResetTokenRepository tokens, IUserRepository users,
    IPasswordResetTokenHasher tokenHasher, IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork, TimeProvider timeProvider) : ICommandHandler<ResetPasswordCommand>
{
    public async Task<Result> Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Token)) return Result.Failure(ResetPasswordErrors.TokenRequired);
        if (string.IsNullOrWhiteSpace(command.NewPassword)) return Result.Failure(ResetPasswordErrors.PasswordRequired);
        if (command.Token.Length != 43 || command.Token.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_'))
            return Result.Failure(ResetPasswordErrors.InvalidToken);

        PasswordResetToken? token = await tokens.GetByHashAsync(tokenHasher.Hash(command.Token), cancellationToken);
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (token is null || !token.IsUsable(now)) return Result.Failure(ResetPasswordErrors.InvalidToken);
        User? user = await users.GetByIdAsync(token.UserId, cancellationToken);
        if (user is null || user.Status is not UserStatus.Active and not UserStatus.Locked)
            return Result.Failure(ResetPasswordErrors.InvalidToken);

        user.ResetPassword(passwordHasher.Hash(command.NewPassword), now);
        token.Consume(now);
        foreach (PasswordResetToken other in await tokens.GetActiveByUserIdAsync(user.Id, cancellationToken))
            if (other.Id != token.Id && !other.IsConsumed && !other.IsRevoked) other.Revoke(now);
        try { await unitOfWork.SaveChangesAsync(cancellationToken); }
        catch (ConcurrencyException) { return Result.Failure(ResetPasswordErrors.InvalidToken); }
        return Result.Success();
    }
}
