using EnterpriseIdentityService.Domain.Users;
namespace EnterpriseIdentityService.UnitTests.Users;
public sealed class UserSessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 12, 10, 0, 0, TimeSpan.Zero);
    [Fact] public void Session_ShouldExpireAndNeverReactivateAfterRevocation()
    {
        UserSession session = UserSession.Create(UserSessionId.New(), UserId.New(), 3, Now, Now.AddDays(30));
        Assert.True(session.IsUsable(Now)); Assert.False(session.IsUsable(Now.AddDays(30)));
        session.Revoke(Now.AddMinutes(1)); session.RecordUse_Throws();
        session.Revoke(Now.AddMinutes(2)); Assert.Equal(Now.AddMinutes(1), session.RevokedAtUtc);
    }
    [Fact] public void RefreshToken_ShouldBeConsumableOnlyOnce()
    {
        RefreshToken token = RefreshToken.Create(RefreshTokenId.New(), UserSessionId.New(), new string('A', 64), Now);
        token.Consume(Now); Assert.True(token.IsConsumed);
        Assert.Throws<InvalidOperationException>(() => token.Consume(Now.AddSeconds(1)));
    }
}
file static class UserSessionAssertions
{
    public static void RecordUse_Throws(this UserSession session) =>
        Assert.Throws<InvalidOperationException>(() => session.RecordUse(DateTimeOffset.MaxValue));
}
