using EnterpriseIdentityService.Application.Abstractions;
using EnterpriseIdentityService.Application.Abstractions.Authentication;
using EnterpriseIdentityService.Application.Abstractions.Mailing;
using EnterpriseIdentityService.Application.Abstractions.Messaging;
using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Application.PasswordRecovery;
using EnterpriseIdentityService.Domain.Users;
using Microsoft.Extensions.Options;

namespace EnterpriseIdentityService.Application.Users.ForgotPassword;

public sealed class ForgotPasswordCommandHandler(
    IUserRepository users, IPasswordResetTokenRepository tokens,
    IPasswordResetTokenGenerator generator, IPasswordResetTokenHasher hasher,
    IPasswordResetEmailFactory emailFactory, IEmailSender emailSender,
    IUnitOfWork unitOfWork, TimeProvider timeProvider,
    IOptions<PasswordRecoveryOptions> options) : ICommandHandler<ForgotPasswordCommand>
{
    public async Task<Result> Handle(ForgotPasswordCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Email)) return Result.Failure(ForgotPasswordErrors.EmailRequired);
        Email email;
        try { email = Email.Create(command.Email); }
        catch (ArgumentException) { return Result.Failure(ForgotPasswordErrors.InvalidEmail); }

        User? user = await users.GetByEmailAsync(email, cancellationToken);
        if (user is null || user.Status is not UserStatus.Active and not UserStatus.Locked) return Result.Success();

        DateTimeOffset now = timeProvider.GetUtcNow();
        IReadOnlyList<PasswordResetToken> active = await tokens.GetActiveByUserIdAsync(user.Id, cancellationToken);
        if (active.OrderByDescending(x => x.CreatedAtUtc).FirstOrDefault() is { } newest &&
            now - newest.CreatedAtUtc < options.Value.RequestCooldown) return Result.Success();

        foreach (PasswordResetToken token in active.Where(x => !x.IsConsumed && !x.IsRevoked)) token.Revoke(now);
        string rawToken = generator.Generate();
        var resetToken = PasswordResetToken.Create(PasswordResetTokenId.New(), user.Id, hasher.Hash(rawToken),
            now, now.Add(options.Value.TokenLifetime));
        tokens.Add(resetToken);
        try { await unitOfWork.SaveChangesAsync(cancellationToken); }
        catch (ConcurrencyException) { return Result.Success(); }

        try { await emailSender.SendAsync(emailFactory.Create(email, rawToken, resetToken.ExpiresAtUtc,
            resetToken.Id.Value.ToString("N")), cancellationToken); }
        catch (EmailDeliveryException) { }
        return Result.Success();
    }
}
