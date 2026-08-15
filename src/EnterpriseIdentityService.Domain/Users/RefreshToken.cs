using EnterpriseIdentityService.Domain.Abstractions;

namespace EnterpriseIdentityService.Domain.Users;

public sealed class RefreshToken : Entity<RefreshTokenId>
{
    private RefreshToken(RefreshTokenId id, UserSessionId sessionId, string tokenHash,
        DateTimeOffset createdAtUtc) : base(id)
    {
        if (string.IsNullOrWhiteSpace(tokenHash)) throw new ArgumentException("Token hash is required.", nameof(tokenHash));
        SessionId = sessionId; TokenHash = tokenHash; CreatedAtUtc = createdAtUtc;
    }
    public UserSessionId SessionId { get; }
    public string TokenHash { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset? ConsumedAtUtc { get; private set; }
    public bool IsConsumed => ConsumedAtUtc.HasValue;
    public static RefreshToken Create(RefreshTokenId id, UserSessionId sessionId,
        string tokenHash, DateTimeOffset createdAtUtc) => new(id, sessionId, tokenHash, createdAtUtc);
    public void Consume(DateTimeOffset occurredOnUtc)
    {
        if (IsConsumed) throw new InvalidOperationException("Refresh token has already been consumed.");
        ConsumedAtUtc = occurredOnUtc;
    }
}
