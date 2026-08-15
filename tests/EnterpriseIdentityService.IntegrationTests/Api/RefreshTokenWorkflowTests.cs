using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using EnterpriseIdentityService.Api.Endpoints.Authentication;
using EnterpriseIdentityService.Api.Endpoints.Users;
using EnterpriseIdentityService.Application.Abstractions.Mailing;
using EnterpriseIdentityService.Application.Abstractions.Authentication;
using EnterpriseIdentityService.Domain.Users;
using EnterpriseIdentityService.Infrastructure.Persistence;
using EnterpriseIdentityService.IntegrationTests.TestDoubles;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.RateLimiting;

namespace EnterpriseIdentityService.IntegrationTests.Api;
public sealed partial class RefreshTokenWorkflowTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task LoginRefreshAndReplay_ShouldRotateThenRevokeEntireSession()
    {
        await factory.ResetDatabaseAsync(); using HttpClient client = CreateClient();
        LoginResponse login = await RegisterVerifyLogin(client, "rotate@example.com", "rotate.user");
        Assert.Equal(43, login.RefreshToken.Length);
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            RefreshToken stored = await db.Set<RefreshToken>().SingleAsync();
            Assert.NotEqual(login.RefreshToken, stored.TokenHash);
            Assert.Equal(64, stored.TokenHash.Length);
        }
        LoginResponse rotated = await ReadLogin(await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(login.RefreshToken)));
        Assert.NotEqual(login.RefreshToken, rotated.RefreshToken);
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            RefreshToken[] tokens = (await db.Set<RefreshToken>().ToArrayAsync()).OrderBy(x => x.CreatedAtUtc).ToArray();
            Assert.Equal(2, tokens.Length);
            Assert.NotNull(tokens[0].ConsumedAtUtc);
            Assert.Null(tokens[1].ConsumedAtUtc);
            Assert.DoesNotContain(tokens, x => x.TokenHash == rotated.RefreshToken);
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(login.RefreshToken))).StatusCode);
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.NotNull((await db.Set<UserSession>().SingleAsync()).RevokedAtUtc);
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(rotated.RefreshToken))).StatusCode);
    }

    [Fact]
    public async Task Logout_ShouldRevokeOnlyCurrentSession_WhileLogoutAllInvalidatesEverySession()
    {
        await factory.ResetDatabaseAsync(); using HttpClient client = CreateClient();
        LoginResponse first = await RegisterVerifyLogin(client, "sessions@example.com", "sessions.user");
        LoginResponse second = await Login(client, "sessions@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", first.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(first.RefreshToken))).StatusCode);
        LoginResponse secondRotated = await ReadLogin(await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(second.RefreshToken)));
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            User user = await db.Set<User>().SingleAsync();
            UserSession[] sessions = (await db.Set<UserSession>().ToArrayAsync()).OrderBy(x => x.CreatedAtUtc).ToArray();
            Assert.Equal(0, user.TokenVersion);
            Assert.NotNull(sessions[0].RevokedAtUtc);
            Assert.Null(sessions[1].RevokedAtUtc);
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secondRotated.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout-all", null)).StatusCode);
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(1, (await db.Set<User>().SingleAsync()).TokenVersion);
            Assert.All(await db.Set<UserSession>().ToArrayAsync(), session => Assert.NotNull(session.RevokedAtUtc));
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users/me")).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(secondRotated.RefreshToken))).StatusCode);
    }

    [Fact]
    public async Task ChangePassword_ShouldInvalidateRefreshSession()
    {
        await factory.ResetDatabaseAsync(); using HttpClient client = CreateClient();
        LoginResponse login = await RegisterVerifyLogin(client, "password@example.com", "password.user");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/users/change-password",
            new ChangePasswordRequest("Password-A!", "Password-B!"))).StatusCode);
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.NotNull((await db.Set<UserSession>().SingleAsync()).RevokedAtUtc);
        }
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(login.RefreshToken))).StatusCode);
    }

    [Theory]
    [InlineData("revoked")]
    [InlineData("locked")]
    [InlineData("disabled")]
    [InlineData("version")]
    public async Task Refresh_ShouldRejectIneligiblePersistedSecurityState(string state)
    {
        await factory.ResetDatabaseAsync(); using HttpClient client = CreateClient();
        LoginResponse login = await RegisterVerifyLogin(client, $"{state}@example.com", $"{state}.user");
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            User user = await db.Set<User>().SingleAsync();
            UserSession session = await db.Set<UserSession>().SingleAsync();
            switch (state)
            {
                case "revoked": session.Revoke(DateTimeOffset.UtcNow); break;
                case "locked": user.Lock(DateTimeOffset.UtcNow); break;
                case "disabled": user.Disable(DateTimeOffset.UtcNow); break;
                case "version": user.InvalidateAuthentication(); break;
            }
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(login.RefreshToken))).StatusCode);
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Single(await db.Set<RefreshToken>().ToArrayAsync());
        }
    }

    [Fact]
    public async Task Refresh_ShouldRejectExpiredSessionWithoutCreatingSuccessor()
    {
        await factory.ResetDatabaseAsync(); using HttpClient client = CreateClient();
        await client.PostAsJsonAsync("/api/auth/register",
            new RegisterUserRequest("expired@example.com", "expired.user", "Password-A!"));
        var sender = factory.Services.GetRequiredService<RecordingEmailSender>();
        string verification = TokenPattern().Match(Assert.Single(sender.Messages).TextBody).Groups[1].Value;
        await client.PostAsJsonAsync("/api/users/verify-email", new VerifyEmailRequest(verification));
        const string raw = "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE";
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            User user = await db.Set<User>().SingleAsync();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            var session = UserSession.Create(UserSessionId.New(), user.Id, user.TokenVersion,
                now.AddDays(-31), now.AddDays(-1));
            db.Set<UserSession>().Add(session);
            db.Set<RefreshToken>().Add(RefreshToken.Create(RefreshTokenId.New(), session.Id,
                scope.ServiceProvider.GetRequiredService<IRefreshTokenHasher>().Hash(raw), now.AddDays(-31)));
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(raw))).StatusCode);
        await using AsyncServiceScope verifyScope = factory.Services.CreateAsyncScope();
        Assert.Single(await verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Set<RefreshToken>().ToArrayAsync());
    }

    [Fact]
    public async Task Refresh_ShouldRejectUnknownMalformedAndMissingCredentialsWithProblemDetails()
    {
        await factory.ResetDatabaseAsync(); using HttpClient client = CreateClient();
        HttpResponseMessage unknown = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(new string('U', 43)));
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal("application/problem+json", unknown.Content.Headers.ContentType?.MediaType);
        Assert.Equal(401, (await unknown.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>())!.Status);
        HttpResponseMessage malformed = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest("bad"));
        Assert.Equal(HttpStatusCode.Unauthorized, malformed.StatusCode);
        HttpResponseMessage missing = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(""));
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(await db.Set<UserSession>().ToArrayAsync());
        Assert.Empty(await db.Set<RefreshToken>().ToArrayAsync());
    }

    [Fact]
    public async Task Refresh_ShouldRejectPendingAccountWithPersistedSession()
    {
        await factory.ResetDatabaseAsync(); using HttpClient client = CreateClient();
        await client.PostAsJsonAsync("/api/auth/register",
            new RegisterUserRequest("pending-refresh@example.com", "pending.refresh", "Password-A!"));
        const string raw = "PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP";
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            User user = await db.Set<User>().SingleAsync();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            var session = UserSession.Create(UserSessionId.New(), user.Id, user.TokenVersion, now, now.AddDays(30));
            db.Set<UserSession>().Add(session);
            db.Set<RefreshToken>().Add(RefreshToken.Create(RefreshTokenId.New(), session.Id,
                scope.ServiceProvider.GetRequiredService<IRefreshTokenHasher>().Hash(raw), now));
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(raw))).StatusCode);
    }

    [Fact]
    public async Task PasswordReset_ShouldInvalidateExistingRefreshSession()
    {
        await factory.ResetDatabaseAsync(); using HttpClient client = CreateClient();
        LoginResponse login = await RegisterVerifyLogin(client, "recovery@example.com", "recovery.user");
        var sender = factory.Services.GetRequiredService<RecordingEmailSender>(); sender.Clear();
        await client.PostAsJsonAsync("/api/users/forgot-password", new ForgotPasswordRequest("recovery@example.com"));
        string reset = TokenPattern().Match(Assert.Single(sender.Messages).TextBody).Groups[1].Value;
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/users/reset-password",
            new ResetPasswordRequest(reset, "Password-B!"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(login.RefreshToken))).StatusCode);
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.NotNull((await db.Set<UserSession>().SingleAsync()).RevokedAtUtc);
    }

    [Fact]
    public async Task Refresh_ShouldApplyTwentyRequestPerIpRateLimit()
    {
        using var isolatedFactory = new ApiWebApplicationFactory();
        await isolatedFactory.ResetDatabaseAsync();
        using HttpClient client = isolatedFactory.CreateClient(
            new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        for (int i = 0; i < 20; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh",
                new RefreshRequest(new string((char)('A' + i), 43)))).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(new string('Z', 43)))).StatusCode);
    }

    [Fact]
    public void LogoutAll_ShouldUseDedicatedSessionSecurityRateLimitPolicy()
    {
        EndpointDataSource source = factory.Services.GetRequiredService<EndpointDataSource>();
        RouteEndpoint endpoint = source.Endpoints.OfType<RouteEndpoint>()
            .Single(x => x.RoutePattern.RawText == "/api/auth/logout-all");
        var metadata = endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>();
        Assert.NotNull(metadata);
        Assert.Equal("session-security", metadata.PolicyName);
    }

    [Fact]
    public async Task LogoutEndpoints_ShouldRequireBearerAuthentication()
    {
        await factory.ResetDatabaseAsync(); using HttpClient client = CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/auth/logout-all", null)).StatusCode);
    }

    private async Task<LoginResponse> RegisterVerifyLogin(HttpClient client, string email, string username)
    {
        await client.PostAsJsonAsync("/api/auth/register", new RegisterUserRequest(email, username, "Password-A!"));
        var sender = factory.Services.GetRequiredService<RecordingEmailSender>();
        string token = TokenPattern().Match(Assert.Single(sender.Messages).TextBody).Groups[1].Value;
        await client.PostAsJsonAsync("/api/users/verify-email", new VerifyEmailRequest(token));
        return await Login(client, email);
    }
    private static async Task<LoginResponse> Login(HttpClient client, string email) =>
        await ReadLogin(await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password-A!")));
    private static async Task<LoginResponse> ReadLogin(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
    }
    private HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
    [GeneratedRegex(@"token=([A-Za-z0-9_-]{43})")] private static partial Regex TokenPattern();
}
