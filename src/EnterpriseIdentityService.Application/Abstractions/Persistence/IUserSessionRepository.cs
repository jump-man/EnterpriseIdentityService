using EnterpriseIdentityService.Domain.Users;
namespace EnterpriseIdentityService.Application.Abstractions.Persistence;
public interface IUserSessionRepository
{
    Task<UserSession?> GetByIdAsync(UserSessionId id, CancellationToken cancellationToken);
    Task<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken);
    Task<IReadOnlyList<UserSession>> GetActiveByUserIdAsync(UserId userId, CancellationToken cancellationToken);
    Task RevokeAsync(UserSessionId id, UserId userId, DateTimeOffset occurredOnUtc,
        CancellationToken cancellationToken);
    void Add(UserSession session);
    void Add(RefreshToken token);
}
