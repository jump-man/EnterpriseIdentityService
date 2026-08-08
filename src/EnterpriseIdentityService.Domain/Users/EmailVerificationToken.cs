using EnterpriseIdentityService.Domain.Abstractions;

namespace EnterpriseIdentityService.Domain.Users;

public sealed class EmailVerificationToken : Entity<EmailVerificationTokenId>
{
    private EmailVerificationToken(
        EmailVerificationTokenId id,
        UserId userId,
        string tokenHash,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException("Token hash is required.", nameof(tokenHash));
        }

        if (expiresAtUtc <= createdAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expiresAtUtc),
                "Expiration must be after creation.");
        }

        UserId = userId;
        TokenHash = tokenHash;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public UserId UserId { get; }
    public string TokenHash { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset ExpiresAtUtc { get; }
    public DateTimeOffset? ConsumedAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public bool IsConsumed => ConsumedAtUtc.HasValue;
    public bool IsRevoked => RevokedAtUtc.HasValue;

    public static EmailVerificationToken Create(
        EmailVerificationTokenId id,
        UserId userId,
        string tokenHash,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc) =>
        new(id, userId, tokenHash, createdAtUtc, expiresAtUtc);

    public bool IsExpired(DateTimeOffset nowUtc) => nowUtc >= ExpiresAtUtc;

    public bool IsUsable(DateTimeOffset nowUtc) =>
        !IsConsumed && !IsRevoked && !IsExpired(nowUtc);

    public void Consume(DateTimeOffset occurredOnUtc)
    {
        if (!IsUsable(occurredOnUtc))
        {
            throw new InvalidOperationException("Only a usable token can be consumed.");
        }

        ConsumedAtUtc = occurredOnUtc;
    }

    public void Revoke(DateTimeOffset occurredOnUtc)
    {
        if (IsConsumed || IsRevoked)
        {
            throw new InvalidOperationException("Only an active token can be revoked.");
        }

        RevokedAtUtc = occurredOnUtc;
    }
}
