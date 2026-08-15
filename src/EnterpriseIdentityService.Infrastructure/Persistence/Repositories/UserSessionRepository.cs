using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Domain.Users;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseIdentityService.Infrastructure.Persistence.Repositories;
internal sealed class UserSessionRepository(ApplicationDbContext db) : IUserSessionRepository
{
    public Task<UserSession?> GetByIdAsync(UserSessionId id, CancellationToken ct) => db.UserSessions.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<RefreshToken?> GetRefreshTokenByHashAsync(string hash, CancellationToken ct) => db.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
    public async Task<IReadOnlyList<UserSession>> GetActiveByUserIdAsync(UserId id, CancellationToken ct) =>
        await db.UserSessions.Where(x => x.UserId == id && x.RevokedAtUtc == null).ToListAsync(ct);
    public async Task RevokeAsync(UserSessionId id, UserId userId, DateTimeOffset occurredOnUtc, CancellationToken ct) =>
        _ = await db.UserSessions
            .Where(x => x.Id == id && x.UserId == userId && x.RevokedAtUtc == null)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.RevokedAtUtc, occurredOnUtc), ct);
    public void Add(UserSession session) => db.UserSessions.Add(session);
    public void Add(RefreshToken token) => db.RefreshTokens.Add(token);
}
