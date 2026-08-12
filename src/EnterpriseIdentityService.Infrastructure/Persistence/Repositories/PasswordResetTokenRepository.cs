using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseIdentityService.Infrastructure.Persistence.Repositories;

internal sealed class PasswordResetTokenRepository(ApplicationDbContext dbContext) : IPasswordResetTokenRepository
{
    public Task<PasswordResetToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        dbContext.PasswordResetTokens.SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
    public async Task<IReadOnlyList<PasswordResetToken>> GetActiveByUserIdAsync(UserId userId, CancellationToken cancellationToken) =>
        await dbContext.PasswordResetTokens.Where(x => x.UserId == userId && x.ConsumedAtUtc == null && x.RevokedAtUtc == null).ToListAsync(cancellationToken);
    public void Add(PasswordResetToken token) => dbContext.PasswordResetTokens.Add(token);
}
