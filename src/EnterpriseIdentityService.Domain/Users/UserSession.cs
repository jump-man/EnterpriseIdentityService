using EnterpriseIdentityService.Domain.Abstractions;

namespace EnterpriseIdentityService.Domain.Users;

public sealed class UserSession : Entity<UserSessionId>
{
    private UserSession(UserSessionId id, UserId userId, int tokenVersionAtIssue,
        DateTimeOffset createdAtUtc, DateTimeOffset expiresAtUtc) : base(id)
    {
        if (expiresAtUtc <= createdAtUtc) throw new ArgumentOutOfRangeException(nameof(expiresAtUtc));
        UserId = userId; TokenVersionAtIssue = tokenVersionAtIssue;
        CreatedAtUtc = createdAtUtc; ExpiresAtUtc = expiresAtUtc; LastUsedAtUtc = createdAtUtc;
    }
    public UserId UserId { get; }
    public int TokenVersionAtIssue { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset ExpiresAtUtc { get; }
    public DateTimeOffset LastUsedAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public bool IsRevoked => RevokedAtUtc.HasValue;
    public static UserSession Create(UserSessionId id, UserId userId, int tokenVersion,
        DateTimeOffset createdAtUtc, DateTimeOffset expiresAtUtc) =>
        new(id, userId, tokenVersion, createdAtUtc, expiresAtUtc);
    public bool IsUsable(DateTimeOffset nowUtc) => !IsRevoked && nowUtc < ExpiresAtUtc;
    public void RecordUse(DateTimeOffset occurredOnUtc)
    {
        if (!IsUsable(occurredOnUtc)) throw new InvalidOperationException("Only an active session can be used.");
        LastUsedAtUtc = occurredOnUtc;
    }
    public void Revoke(DateTimeOffset occurredOnUtc)
    {
        if (!IsRevoked) RevokedAtUtc = occurredOnUtc;
    }
}
