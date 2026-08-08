using EnterpriseIdentityService.Application.Abstractions;
using EnterpriseIdentityService.Application.Abstractions.Authentication;
using EnterpriseIdentityService.Application.Abstractions.Messaging;
using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Domain.Users;

namespace EnterpriseIdentityService.Application.Users.VerifyEmail;

public sealed class VerifyEmailCommandHandler(
    IEmailVerificationTokenRepository tokenRepository,
    IUserRepository userRepository,
    IEmailVerificationTokenHasher tokenHasher,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<VerifyEmailCommand>
{
    public async Task<Result> Handle(
        VerifyEmailCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.Token))
        {
            return Result.Failure(VerifyEmailErrors.TokenRequired);
        }

        if (command.Token.Length != 43 ||
            command.Token.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
        {
            return Result.Failure(VerifyEmailErrors.InvalidToken);
        }

        EmailVerificationToken? token = await tokenRepository.GetByHashAsync(
            tokenHasher.Hash(command.Token), cancellationToken);
        DateTimeOffset nowUtc = timeProvider.GetUtcNow();

        if (token is null || !token.IsUsable(nowUtc))
        {
            return Result.Failure(VerifyEmailErrors.InvalidToken);
        }

        User? user = await userRepository.GetByIdAsync(token.UserId, cancellationToken);
        if (user is null || user.Status != UserStatus.Pending)
        {
            return Result.Failure(VerifyEmailErrors.InvalidToken);
        }

        user.VerifyEmail(nowUtc);
        token.Consume(nowUtc);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyException)
        {
            return Result.Failure(VerifyEmailErrors.InvalidToken);
        }

        return Result.Success();
    }
}
