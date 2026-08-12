using EnterpriseIdentityService.Domain.Users;

namespace EnterpriseIdentityService.Application.Abstractions.Persistence;

public interface IPasswordResetTokenRepository
{
    Task<PasswordResetToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken);
    Task<IReadOnlyList<PasswordResetToken>> GetActiveByUserIdAsync(UserId userId, CancellationToken cancellationToken);
    void Add(PasswordResetToken token);
}
