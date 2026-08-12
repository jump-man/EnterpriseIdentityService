using EnterpriseIdentityService.Domain.Users;

namespace EnterpriseIdentityService.UnitTests.Users;

public sealed class PasswordResetTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 12, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_ShouldBeUsableBeforeExpiration() => Assert.True(Create().IsUsable(Now.AddMinutes(1)));

    [Fact]
    public void Consume_ShouldPreventReplay()
    {
        PasswordResetToken token = Create();
        token.Consume(Now.AddMinutes(1));
        Assert.False(token.IsUsable(Now.AddMinutes(1)));
        Assert.Throws<InvalidOperationException>(() => token.Consume(Now.AddMinutes(2)));
    }

    [Fact]
    public void Revoke_ShouldMakeTokenUnusable()
    {
        PasswordResetToken token = Create();
        token.Revoke(Now.AddMinutes(1));
        Assert.True(token.IsRevoked);
        Assert.False(token.IsUsable(Now.AddMinutes(1)));
    }

    private static PasswordResetToken Create() => PasswordResetToken.Create(
        PasswordResetTokenId.New(), UserId.New(), new string('A', 64), Now, Now.AddMinutes(15));
}
