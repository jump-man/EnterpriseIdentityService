using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Domain.Users;
using EnterpriseIdentityService.Infrastructure.Persistence;
using EnterpriseIdentityService.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseIdentityService.IntegrationTests.Infrastructure.Persistence;

public sealed class RefreshConcurrencyTests
{
    [Fact]
    public async Task DuplicateRotation_ShouldPersistAtMostOneSuccessor()
    {
        await using SqliteTestDatabase database = await SqliteTestDatabase.CreateAsync();
        UserId userId = UserId.New();
        UserSessionId sessionId = UserSessionId.New();
        RefreshTokenId predecessorId = RefreshTokenId.New();
        DateTimeOffset now = new(2026, 8, 12, 10, 0, 0, TimeSpan.Zero);
        await using (ApplicationDbContext seed = database.CreateContext())
        {
            User user = User.Register(userId, Email.Create("race@example.com"), Username.Create("race.user"),
                PasswordHash.Create("HASH"), now);
            user.VerifyEmail(now);
            seed.Users.Add(user);
            seed.UserSessions.Add(UserSession.Create(sessionId, userId, 0, now, now.AddDays(30)));
            seed.RefreshTokens.Add(RefreshToken.Create(predecessorId, sessionId, new string('A', 64), now));
            await seed.SaveChangesAsync();
        }

        await using ApplicationDbContext first = database.CreateContext();
        await using ApplicationDbContext second = database.CreateContext();
        RefreshToken firstToken = await first.RefreshTokens.SingleAsync(x => x.Id == predecessorId);
        RefreshToken secondToken = await second.RefreshTokens.SingleAsync(x => x.Id == predecessorId);
        UserSession firstSession = await first.UserSessions.SingleAsync(x => x.Id == sessionId);
        UserSession secondSession = await second.UserSessions.SingleAsync(x => x.Id == sessionId);
        firstToken.Consume(now.AddMinutes(1)); firstSession.RecordUse(now.AddMinutes(1));
        secondToken.Consume(now.AddMinutes(1)); secondSession.RecordUse(now.AddMinutes(1));
        first.RefreshTokens.Add(RefreshToken.Create(RefreshTokenId.New(), sessionId, new string('B', 64), now.AddMinutes(1)));
        second.RefreshTokens.Add(RefreshToken.Create(RefreshTokenId.New(), sessionId, new string('C', 64), now.AddMinutes(1)));

        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<ConcurrencyException>(() => second.SaveChangesAsync());

        await using ApplicationDbContext verify = database.CreateContext();
        RefreshToken[] persisted = await verify.RefreshTokens.ToArrayAsync();
        Assert.Equal(2, persisted.Length);
        Assert.Single(persisted, token => token.Id != predecessorId && !token.IsConsumed);
    }

    [Fact]
    public async Task AtomicLogout_ShouldPreventStaleRefreshFromCommitting()
    {
        await using SqliteTestDatabase database = await SqliteTestDatabase.CreateAsync();
        UserId userId = UserId.New(); UserSessionId sessionId = UserSessionId.New();
        DateTimeOffset now = new(2026, 8, 12, 11, 0, 0, TimeSpan.Zero);
        await using (ApplicationDbContext seed = database.CreateContext())
        {
            User user = User.Register(userId, Email.Create("logout-race@example.com"),
                Username.Create("logout.race"), PasswordHash.Create("HASH"), now);
            user.VerifyEmail(now); seed.Users.Add(user);
            seed.UserSessions.Add(UserSession.Create(sessionId, userId, 0, now, now.AddDays(30)));
            seed.RefreshTokens.Add(RefreshToken.Create(RefreshTokenId.New(), sessionId, new string('D', 64), now));
            await seed.SaveChangesAsync();
        }

        await using ApplicationDbContext staleRefresh = database.CreateContext();
        UserSession staleSession = await staleRefresh.UserSessions.SingleAsync();
        RefreshToken staleToken = await staleRefresh.RefreshTokens.SingleAsync();
        staleToken.Consume(now.AddMinutes(1)); staleSession.RecordUse(now.AddMinutes(1));
        staleRefresh.RefreshTokens.Add(RefreshToken.Create(
            RefreshTokenId.New(), sessionId, new string('E', 64), now.AddMinutes(1)));

        await using (ApplicationDbContext logoutContext = database.CreateContext())
        {
            var repository = new UserSessionRepository(logoutContext);
            await repository.RevokeAsync(sessionId, userId, now.AddSeconds(30), CancellationToken.None);
        }
        await Assert.ThrowsAsync<ConcurrencyException>(() => staleRefresh.SaveChangesAsync());

        await using ApplicationDbContext verify = database.CreateContext();
        Assert.NotNull((await verify.UserSessions.SingleAsync()).RevokedAtUtc);
        Assert.Single(await verify.RefreshTokens.ToArrayAsync());
    }
}
