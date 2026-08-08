using EnterpriseIdentityService.Domain.Users;

namespace EnterpriseIdentityService.UnitTests.Users;

public sealed class EmailVerificationTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 5, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_ShouldBeUsableBeforeExpiration()
    {
        EmailVerificationToken token = CreateToken();

        Assert.True(token.IsUsable(Now.AddMinutes(1)));
        Assert.False(token.IsConsumed);
        Assert.False(token.IsRevoked);
    }

    [Fact]
    public void IsUsable_ShouldBeFalseAtExpirationBoundary()
    {
        EmailVerificationToken token = CreateToken();

        Assert.True(token.IsExpired(Now.AddHours(1)));
        Assert.False(token.IsUsable(Now.AddHours(1)));
    }

    [Fact]
    public void Consume_ShouldRecordTimestampAndRejectSecondConsumption()
    {
        EmailVerificationToken token = CreateToken();
        DateTimeOffset consumedAt = Now.AddMinutes(5);

        token.Consume(consumedAt);

        Assert.Equal(consumedAt, token.ConsumedAtUtc);
        Assert.False(token.IsUsable(consumedAt));
        Assert.Throws<InvalidOperationException>(() => token.Consume(consumedAt));
    }

    [Fact]
    public void Revoke_ShouldRecordTimestampAndMakeTokenUnusable()
    {
        EmailVerificationToken token = CreateToken();
        DateTimeOffset revokedAt = Now.AddMinutes(5);

        token.Revoke(revokedAt);

        Assert.Equal(revokedAt, token.RevokedAtUtc);
        Assert.False(token.IsUsable(revokedAt));
    }

    private static EmailVerificationToken CreateToken() =>
        EmailVerificationToken.Create(
            EmailVerificationTokenId.New(), UserId.New(), new string('a', 64),
            Now, Now.AddHours(1));
}
