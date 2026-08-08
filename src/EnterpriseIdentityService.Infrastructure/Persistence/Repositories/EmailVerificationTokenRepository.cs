using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseIdentityService.Infrastructure.Persistence.Repositories;

internal sealed class EmailVerificationTokenRepository(ApplicationDbContext dbContext)
    : IEmailVerificationTokenRepository
{
    public Task<EmailVerificationToken?> GetByHashAsync(
        string tokenHash, CancellationToken cancellationToken) =>
        dbContext.EmailVerificationTokens.SingleOrDefaultAsync(
            token => token.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<EmailVerificationToken>> GetActiveByUserIdAsync(
        UserId userId, CancellationToken cancellationToken) =>
        await dbContext.EmailVerificationTokens
            .Where(token => token.UserId == userId &&
                token.ConsumedAtUtc == null && token.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

    public void Add(EmailVerificationToken token) =>
        dbContext.EmailVerificationTokens.Add(token);
}
