using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using EnterpriseIdentityService.Api.Endpoints.Authentication;
using EnterpriseIdentityService.Api.Endpoints.Users;
using EnterpriseIdentityService.Domain.Users;
using EnterpriseIdentityService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace EnterpriseIdentityService.IntegrationTests.Api;

public sealed class AuthenticationEndpointTests(ApiWebApplicationFactory factory)
    : IClassFixture<ApiWebApplicationFactory>
{
    private const string Password = "StrongPassword123!";

    [Fact]
    public async Task LoginAndCurrentUser_ShouldSucceed_ForActiveRegisteredUser()
    {
        await factory.ResetDatabaseAsync();
        using HttpClient client = CreateClient();
        Guid userId = await RegisterAndActivateAsync(client);

        HttpResponseMessage loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest("USER@example.com", Password));

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        LoginResponse? login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(login);
        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));
        Assert.Equal("Bearer", login.TokenType);
        Assert.InRange(login.ExpiresIn, 1, 900);

        new JwtSecurityTokenHandler().ValidateToken(
            login.AccessToken,
            new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                    "test-only-signing-key-with-at-least-thirty-two-characters")),
                ValidateIssuer = true,
                ValidIssuer = "EnterpriseIdentityService.Tests",
                ValidateAudience = true,
                ValidAudience = "EnterpriseIdentityService.Tests.Client",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            },
            out _);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);
        HttpResponseMessage meResponse = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        CurrentUserResponse? currentUser =
            await meResponse.Content.ReadFromJsonAsync<CurrentUserResponse>();
        Assert.NotNull(currentUser);
        Assert.Equal(userId, currentUser.Id);
        Assert.Equal("user@example.com", currentUser.Email);
        Assert.Equal("Active", currentUser.Status);
    }

    [Fact]
    public async Task Login_ShouldReturnEquivalentGenericProblems_ForUnknownUserAndWrongPassword()
    {
        await factory.ResetDatabaseAsync();
        using HttpClient client = CreateClient();
        await RegisterAndActivateAsync(client);

        HttpResponseMessage unknown = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest("unknown@example.com", Password));
        HttpResponseMessage wrongPassword = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest("user@example.com", "wrong-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        ProblemDetails? first = await unknown.Content.ReadFromJsonAsync<ProblemDetails>();
        ProblemDetails? second = await wrongPassword.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.Title, second.Title);
        Assert.Equal(first.Detail, second.Detail);
        Assert.Equal(
            Assert.IsType<JsonElement>(first.Extensions["errorCode"]).GetString(),
            Assert.IsType<JsonElement>(second.Extensions["errorCode"]).GetString());
    }

    [Fact]
    public async Task Login_ShouldRejectPendingUserWithGenericCredentialsError()
    {
        await factory.ResetDatabaseAsync();
        using HttpClient client = CreateClient();
        await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterUserRequest("user@example.com", "ali.dev", Password));

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest("user@example.com", Password));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-token")]
    public async Task CurrentUser_ShouldReturnUnauthorized_WhenTokenIsMissingOrMalformed(
        string? token)
    {
        using HttpClient client = CreateClient();
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        HttpResponseMessage response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Swagger_ShouldExposeBearerSchemeAndAuthenticationEndpoints()
    {
        using HttpClient client = CreateClient();

        string document = await client.GetStringAsync("/swagger/v1/swagger.json");

        Assert.Contains("/api/auth/login", document, StringComparison.Ordinal);
        Assert.Contains("/api/users/me", document, StringComparison.Ordinal);
        Assert.Contains("/api/permissions", document, StringComparison.Ordinal);
        Assert.Contains("/api/roles", document, StringComparison.Ordinal);
        Assert.Contains("/api/users/{userId}/roles", document, StringComparison.Ordinal);
        Assert.Contains("\"Bearer\"", document, StringComparison.Ordinal);
        Assert.Contains("\"scheme\": \"bearer\"", document, StringComparison.Ordinal);
    }

    private HttpClient CreateClient() => factory.CreateClient(
        new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    private async Task<Guid> RegisterAndActivateAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterUserRequest("user@example.com", "ali.dev", Password));
        RegisterUserResponse? registered =
            await response.Content.ReadFromJsonAsync<RegisterUserResponse>();
        Assert.NotNull(registered);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context =
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        User user = await context.Set<User>().SingleAsync();
        user.VerifyEmail(DateTimeOffset.UtcNow);
        await context.SaveChangesAsync();

        return registered.UserId;
    }
}
