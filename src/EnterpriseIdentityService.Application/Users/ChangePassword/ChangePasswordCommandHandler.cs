using EnterpriseIdentityService.Application.Abstractions;
using EnterpriseIdentityService.Application.Abstractions.Authentication;
using EnterpriseIdentityService.Application.Abstractions.Messaging;
using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Domain.Users;

namespace EnterpriseIdentityService.Application.Users.ChangePassword;

public sealed class ChangePasswordCommandHandler(
    IUserRepository users,
    IPasswordResetTokenRepository resetTokens,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<ChangePasswordCommand>
{
    public async Task<Result> Handle(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(command.CurrentPassword))
            return Result.Failure(ChangePasswordErrors.CurrentPasswordRequired);
        if (string.IsNullOrWhiteSpace(command.NewPassword))
            return Result.Failure(ChangePasswordErrors.NewPasswordRequired);

        User? user = await users.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null) return Result.Failure(ChangePasswordErrors.UserNotFound);
        if (user.Status != UserStatus.Active) return Result.Failure(ChangePasswordErrors.Forbidden);
        if (!passwordHasher.Verify(command.CurrentPassword, user.PasswordHash))
            return Result.Failure(ChangePasswordErrors.InvalidCurrentPassword);
        if (passwordHasher.Verify(command.NewPassword, user.PasswordHash))
            return Result.Failure(ChangePasswordErrors.SamePassword);

        DateTimeOffset now = timeProvider.GetUtcNow();
        user.ChangePassword(passwordHasher.Hash(command.NewPassword), now);
        foreach (PasswordResetToken token in await resetTokens.GetActiveByUserIdAsync(user.Id, cancellationToken))
            if (!token.IsConsumed && !token.IsRevoked) token.Revoke(now);

        try { await unitOfWork.SaveChangesAsync(cancellationToken); }
        catch (ConcurrencyException) { return Result.Failure(ChangePasswordErrors.ConcurrencyConflict); }
        return Result.Success();
    }
}
