using EnterpriseIdentityService.Application.Abstractions;
using EnterpriseIdentityService.Application.Abstractions.Authentication;
using EnterpriseIdentityService.Application.Abstractions.Mailing;
using EnterpriseIdentityService.Application.Abstractions.Messaging;
using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Application.EmailVerification;
using EnterpriseIdentityService.Domain.Users;
using Microsoft.Extensions.Options;

namespace EnterpriseIdentityService.Application.Users.ResendVerificationEmail;

public sealed class ResendVerificationEmailCommandHandler(
    IUserRepository userRepository,
    IEmailVerificationTokenRepository tokenRepository,
    IEmailVerificationTokenGenerator tokenGenerator,
    IEmailVerificationTokenHasher tokenHasher,
    IVerificationEmailFactory emailFactory,
    IEmailSender emailSender,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IOptions<EmailVerificationOptions> options) : ICommandHandler<ResendVerificationEmailCommand>
{
    public async Task<Result> Handle(
        ResendVerificationEmailCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(command.Email))
        {
            return Result.Failure(ResendVerificationEmailErrors.EmailRequired);
        }

        Email email;
        try { email = Email.Create(command.Email); }
        catch (ArgumentException) { return Result.Failure(ResendVerificationEmailErrors.InvalidEmail); }

        User? user = await userRepository.GetByEmailAsync(email, cancellationToken);
        if (user is null || user.Status != UserStatus.Pending)
        {
            return Result.Success();
        }

        DateTimeOffset nowUtc = timeProvider.GetUtcNow();
        IReadOnlyList<EmailVerificationToken> activeTokens =
            await tokenRepository.GetActiveByUserIdAsync(user.Id, cancellationToken);

        EmailVerificationToken? newest = activeTokens
            .OrderByDescending(token => token.CreatedAtUtc)
            .FirstOrDefault();
        if (newest is not null && nowUtc - newest.CreatedAtUtc < options.Value.ResendCooldown)
        {
            return Result.Success();
        }

        foreach (EmailVerificationToken activeToken in activeTokens)
        {
            if (!activeToken.IsConsumed && !activeToken.IsRevoked)
            {
                activeToken.Revoke(nowUtc);
            }
        }

        string rawToken = tokenGenerator.Generate();
        var token = EmailVerificationToken.Create(
            EmailVerificationTokenId.New(), user.Id, tokenHasher.Hash(rawToken),
            nowUtc, nowUtc.Add(options.Value.TokenLifetime));
        tokenRepository.Add(token);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            await emailSender.SendAsync(
                emailFactory.Create(email, rawToken, token.ExpiresAtUtc, token.Id.Value.ToString("N")),
                cancellationToken);
        }
        catch (EmailDeliveryException)
        {
            // Preserve the generic response to prevent account enumeration. The
            // pending account and its committed token remain recoverable.
            return Result.Success();
        }

        return Result.Success();
    }
}
