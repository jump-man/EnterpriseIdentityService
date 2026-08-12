using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using EnterpriseIdentityService.Api.Endpoints.Authentication;
using EnterpriseIdentityService.Api.Endpoints.Users;
using EnterpriseIdentityService.Application.Abstractions.Mailing;
using EnterpriseIdentityService.IntegrationTests.TestDoubles;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseIdentityService.IntegrationTests.Api;

public sealed partial class ChangePasswordWorkflowTests(ApiWebApplicationFactory factory)
    : IClassFixture<ApiWebApplicationFactory>
{
    private const string PasswordA = "Password-A!";
    private const string PasswordB = "Password-B!";

    [Fact]
    public async Task ChangePassword_ShouldInvalidateOldPasswordJwtAndOutstandingResetToken()
    {
        await factory.ResetDatabaseAsync();
        using HttpClient client = CreateClient();
        LoginResponse login = await RegisterVerifyAndLogin(client, "change@example.com", "change.user");

        RecordingEmailSender sender = factory.Services.GetRequiredService<RecordingEmailSender>();
        sender.Clear();
        await client.PostAsJsonAsync("/api/users/forgot-password", new ForgotPasswordRequest("change@example.com"));
        string resetToken = TokenPattern().Match(Assert.Single(sender.Messages).TextBody).Groups[1].Value;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        HttpResponseMessage changed = await client.PostAsJsonAsync("/api/users/change-password",
            new ChangePasswordRequest(PasswordA, PasswordB));
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users/me")).StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("change@example.com", PasswordA))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("change@example.com", PasswordB))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/users/reset-password",
            new ResetPasswordRequest(resetToken, "Password-C!"))).StatusCode);
    }

    [Fact]
    public async Task ChangePassword_ShouldRequireAuthentication()
    {
        await factory.ResetDatabaseAsync();
        using HttpClient client = CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/users/change-password",
            new ChangePasswordRequest(PasswordA, PasswordB))).StatusCode);
    }

    [Fact]
    public async Task ChangePassword_ShouldRejectWrongAndSamePasswordWithoutChangingSecurityState()
    {
        await factory.ResetDatabaseAsync();
        using HttpClient client = CreateClient();
        LoginResponse login = await RegisterVerifyAndLogin(client, "failure@example.com", "failure.user");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        HttpResponseMessage wrong = await client.PostAsJsonAsync("/api/users/change-password",
            new ChangePasswordRequest("Wrong-Password!", PasswordB));
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        ProblemDetails problem = (await wrong.Content.ReadFromJsonAsync<ProblemDetails>())!;
        Assert.Equal(400, problem.Status);

        HttpResponseMessage same = await client.PostAsJsonAsync("/api/users/change-password",
            new ChangePasswordRequest(PasswordA, PasswordA));
        Assert.Equal(HttpStatusCode.BadRequest, same.StatusCode);

        HttpResponseMessage missingNewPassword = await client.PostAsJsonAsync("/api/users/change-password",
            new ChangePasswordRequest(PasswordA, ""));
        Assert.Equal(HttpStatusCode.BadRequest, missingNewPassword.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/users/me")).StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("failure@example.com", PasswordA))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("failure@example.com", PasswordB))).StatusCode);
    }

    [Fact]
    public async Task ChangePassword_ShouldRateLimitRepeatedAttemptsPerUser()
    {
        await factory.ResetDatabaseAsync();
        using HttpClient client = CreateClient();
        LoginResponse login = await RegisterVerifyAndLogin(client, "limit@example.com", "limit.user");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        HttpResponseMessage? response = null;
        for (int i = 0; i < 6; i++)
            response = await client.PostAsJsonAsync("/api/users/change-password",
                new ChangePasswordRequest("Wrong-Password!", PasswordB));
        Assert.Equal(HttpStatusCode.TooManyRequests, response!.StatusCode);
    }

    private async Task<LoginResponse> RegisterVerifyAndLogin(HttpClient client, string email, string username)
    {
        await client.PostAsJsonAsync("/api/auth/register", new RegisterUserRequest(email, username, PasswordA));
        RecordingEmailSender sender = factory.Services.GetRequiredService<RecordingEmailSender>();
        string token = TokenPattern().Match(Assert.Single(sender.Messages).TextBody).Groups[1].Value;
        await client.PostAsJsonAsync("/api/users/verify-email", new VerifyEmailRequest(token));
        return (await (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, PasswordA)))
            .Content.ReadFromJsonAsync<LoginResponse>())!;
    }

    private HttpClient CreateClient() => factory.CreateClient(
        new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    [GeneratedRegex(@"token=([A-Za-z0-9_-]{43})")]
    private static partial Regex TokenPattern();
}
