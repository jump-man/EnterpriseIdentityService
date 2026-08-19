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

public sealed partial class PasswordRecoveryWorkflowTests(ApiWebApplicationFactory factory)
    : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Reset_ShouldChangePasswordConsumeTokenAndInvalidateOldJwt()
    {
        await factory.ResetDatabaseAsync();
        using HttpClient client = CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", new RegisterUserRequest("reset@example.com", "reset.user", "Password-A!"));
        RecordingEmailSender sender = factory.Services.GetRequiredService<RecordingEmailSender>();
        string verifyToken = TokenPattern().Match(Assert.Single(sender.Messages).TextBody).Groups[1].Value;
        await client.PostAsJsonAsync("/api/users/verify-email", new VerifyEmailRequest(verifyToken));
        LoginResponse oldLogin = (await (await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("reset@example.com", "Password-A!"))).Content.ReadFromJsonAsync<LoginResponse>())!;

        sender.Clear();
        HttpResponseMessage forgot = await client.PostAsJsonAsync("/api/users/forgot-password", new ForgotPasswordRequest("reset@example.com"));
        Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);
        string resetToken = TokenPattern().Match(Assert.Single(sender.Messages).TextBody).Groups[1].Value;

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            PasswordResetToken stored = await context.Set<PasswordResetToken>().SingleAsync();
            Assert.NotEqual(resetToken, stored.TokenHash);
        }

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/users/reset-password",
            new ResetPasswordRequest(resetToken, "Password-B!"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/users/reset-password",
            new ResetPasswordRequest(resetToken, "Password-C!"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("reset@example.com", "Password-A!"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("reset@example.com", "Password-B!"))).StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oldLogin.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users/me")).StatusCode);

        await using AsyncServiceScope auditScope = factory.Services.CreateAsyncScope();
        ApplicationDbContext auditContext =
            auditScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        AuditEventType[] auditEvents = await auditContext.Set<AuditEntry>()
            .Where(entry => entry.EventType == AuditEventType.PasswordResetRequested ||
                            entry.EventType == AuditEventType.PasswordResetCompleted)
            .Select(entry => entry.EventType)
            .ToArrayAsync();
        Assert.Contains(AuditEventType.PasswordResetRequested, auditEvents);
        Assert.Contains(AuditEventType.PasswordResetCompleted, auditEvents);
        string persistedAuditText = string.Join('|', (await auditContext.Set<AuditEntry>().ToArrayAsync())
            .SelectMany(entry => new[] { entry.CorrelationId, entry.UserAgent, entry.Permission })
            .OfType<string>());
        Assert.DoesNotContain(resetToken, persistedAuditText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Forgot_ShouldBeGenericForUnknownAndPendingAccounts()
    {
        await factory.ResetDatabaseAsync();
        using HttpClient client = CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", new RegisterUserRequest("pending@example.com", "pending.user", "Password-A!"));
        factory.Services.GetRequiredService<RecordingEmailSender>().Clear();
        HttpResponseMessage unknown = await client.PostAsJsonAsync("/api/users/forgot-password", new ForgotPasswordRequest("unknown@example.com"));
        HttpResponseMessage pending = await client.PostAsJsonAsync("/api/users/forgot-password", new ForgotPasswordRequest("pending@example.com"));
        Assert.Equal(HttpStatusCode.Accepted, unknown.StatusCode);
        Assert.Equal(unknown.StatusCode, pending.StatusCode);
        Assert.Empty(factory.Services.GetRequiredService<RecordingEmailSender>().Messages);
    }

    private HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
    [GeneratedRegex(@"token=([A-Za-z0-9_-]{43})")]
    private static partial Regex TokenPattern();
}
