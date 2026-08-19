using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using EnterpriseIdentityService.Api.Endpoints.Authentication;
using EnterpriseIdentityService.Api.Endpoints.Users;
using EnterpriseIdentityService.Application.Abstractions.Mailing;
using EnterpriseIdentityService.Domain.Users;
using EnterpriseIdentityService.Infrastructure.Persistence;
using EnterpriseIdentityService.IntegrationTests.TestDoubles;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EnterpriseIdentityService.Domain.Auditing;

namespace EnterpriseIdentityService.IntegrationTests.Api;

public sealed partial class EmailVerificationWorkflowTests(ApiWebApplicationFactory factory)
    : IClassFixture<ApiWebApplicationFactory>
{
    private const string Password = "StrongPassword123!";

    [Fact]
    public async Task RegisterVerifyLoginAndCurrentUser_ShouldCompleteSecureWorkflow()
    {
        await factory.ResetDatabaseAsync();
        using HttpClient client = CreateClient();

        HttpResponseMessage registration = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterUserRequest("user@example.com", "ali.dev", Password));
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        Assert.DoesNotContain("token", await registration.Content.ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase);

        RecordingEmailSender sender = factory.Services.GetRequiredService<RecordingEmailSender>();
        EmailMessage email = Assert.Single(sender.Messages);
        string rawToken = TokenPattern().Match(email.TextBody).Groups[1].Value;
        Assert.Equal(43, rawToken.Length);

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            User user = await context.Set<User>().SingleAsync();
            EmailVerificationToken storedToken = await context.Set<EmailVerificationToken>().SingleAsync();
            Assert.Equal(UserStatus.Pending, user.Status);
            Assert.Null(user.EmailVerifiedAtUtc);
            Assert.NotEqual(rawToken, storedToken.TokenHash);
            Assert.DoesNotContain(rawToken, storedToken.TokenHash, StringComparison.Ordinal);
        }

        HttpResponseMessage loginBefore = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest("user@example.com", Password));
        Assert.Equal(HttpStatusCode.Unauthorized, loginBefore.StatusCode);

        HttpResponseMessage verification = await client.PostAsJsonAsync(
            "/api/users/verify-email", new VerifyEmailRequest(rawToken));
        Assert.Equal(HttpStatusCode.NoContent, verification.StatusCode);

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            User user = await context.Set<User>().SingleAsync();
            EmailVerificationToken storedToken = await context.Set<EmailVerificationToken>().SingleAsync();
            Assert.Equal(UserStatus.Active, user.Status);
            Assert.NotNull(user.EmailVerifiedAtUtc);
            Assert.NotNull(storedToken.ConsumedAtUtc);
            AuditEntry verified = await context.Set<AuditEntry>().SingleAsync(
                entry => entry.EventType == AuditEventType.EmailVerified);
            Assert.Equal(user.Id, verified.TargetUserId);
            Assert.DoesNotContain(rawToken,
                string.Join('|', verified.CorrelationId, verified.UserAgent, verified.Permission),
                StringComparison.Ordinal);
        }

        HttpResponseMessage reuse = await client.PostAsJsonAsync(
            "/api/users/verify-email", new VerifyEmailRequest(rawToken));
        Assert.Equal(HttpStatusCode.BadRequest, reuse.StatusCode);

        HttpResponseMessage loginAfter = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest("user@example.com", Password));
        Assert.Equal(HttpStatusCode.OK, loginAfter.StatusCode);
        LoginResponse? login = await loginAfter.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/users/me")).StatusCode);
    }

    [Fact]
    public async Task Resend_ShouldBeGenericAndHonorCooldownForPendingUser()
    {
        await factory.ResetDatabaseAsync();
        using HttpClient client = CreateClient();
        await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterUserRequest("user@example.com", "ali.dev", Password));

        HttpResponseMessage unknown = await client.PostAsJsonAsync(
            "/api/users/resend-verification-email",
            new ResendVerificationEmailRequest("unknown@example.com"));
        HttpResponseMessage pending = await client.PostAsJsonAsync(
            "/api/users/resend-verification-email",
            new ResendVerificationEmailRequest("user@example.com"));

        Assert.Equal(HttpStatusCode.NoContent, unknown.StatusCode);
        Assert.Equal(unknown.StatusCode, pending.StatusCode);
        Assert.Equal(await unknown.Content.ReadAsStringAsync(), await pending.Content.ReadAsStringAsync());
        Assert.Single(factory.Services.GetRequiredService<RecordingEmailSender>().Messages);
    }

    private HttpClient CreateClient() => factory.CreateClient(
        new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    [GeneratedRegex(@"token=([A-Za-z0-9_-]{43})")]
    private static partial Regex TokenPattern();
}
