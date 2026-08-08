using EnterpriseIdentityService.Domain.Users;

namespace EnterpriseIdentityService.Application.Abstractions.Persistence;

public interface IEmailVerificationTokenRepository
{
    Task<EmailVerificationToken?> GetByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<EmailVerificationToken>> GetActiveByUserIdAsync(
        UserId userId,
        CancellationToken cancellationToken);

    void Add(EmailVerificationToken token);
}
