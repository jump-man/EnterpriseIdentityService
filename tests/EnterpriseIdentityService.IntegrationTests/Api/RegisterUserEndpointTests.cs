using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EnterpriseIdentityService.Api.Endpoints.Authentication;
using EnterpriseIdentityService.Domain.Users;
using EnterpriseIdentityService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseIdentityService.IntegrationTests.Api;

public sealed class RegisterUserEndpointTests(
    ApiWebApplicationFactory factory)
    : IClassFixture<ApiWebApplicationFactory>
{
    private const string PlaintextPassword = "StrongPassword123!";

    [Fact]
    public async Task Register_ShouldReturnCreatedAndPersistHashedPassword()
    {
        await factory.ResetDatabaseAsync();
        using HttpClient client = CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/register",
            ValidRequest());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        RegisterUserResponse? body =
            await response.Content.ReadFromJsonAsync<RegisterUserResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.UserId);
        Assert.Equal($"/api/users/{body.UserId}", response.Headers.Location?.OriginalString);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context =
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        User user = await context.Set<User>().SingleAsync();

        Assert.Equal(body.UserId, user.Id.Value);
        Assert.Equal("user@example.com", user.Email.Value);
        Assert.Equal("ali.dev", user.Username.Value);
        Assert.False(string.IsNullOrWhiteSpace(user.PasswordHash.Value));
        Assert.NotEqual(PlaintextPassword, user.PasswordHash.Value);
    }

    [Fact]
    public async Task Register_ShouldReturnConflictProblemDetails_WhenEmailIsDuplicate()
    {
        await factory.ResetDatabaseAsync();
        using HttpClient client = CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", ValidRequest());

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterUserRequest(
                "USER@example.com",
                "different.user",
                PlaintextPassword));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(409, problem.Status);
        JsonElement errorCode = Assert.IsType<JsonElement>(problem.Extensions["errorCode"]);
        Assert.Equal("Users.Register.EmailAlreadyInUse", errorCode.GetString());
        Assert.Equal(1, await CountUsersAsync());
    }

    [Fact]
    public async Task Register_ShouldReturnBadRequestAndPersistNothing_WhenEmailIsInvalid()
    {
        await factory.ResetDatabaseAsync();
        using HttpClient client = CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterUserRequest("invalid", "ali.dev", PlaintextPassword));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(0, await CountUsersAsync());
    }

    [Fact]
    public async Task Register_ShouldReturnBadRequest_WhenRequiredFieldsAreMissing()
    {
        await factory.ResetDatabaseAsync();
        using HttpClient client = CreateClient();

        HttpResponseMessage response = await client.PostAsync(
            "/api/auth/register",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(0, await CountUsersAsync());
    }

    [Fact]
    public async Task Register_ShouldReturnBadRequest_WhenJsonIsInvalid()
    {
        await factory.ResetDatabaseAsync();
        using HttpClient client = CreateClient();

        HttpResponseMessage response = await client.PostAsync(
            "/api/auth/register",
            new StringContent("{ invalid", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(0, await CountUsersAsync());
    }

    [Fact]
    public async Task Swagger_ShouldExposeRegistrationEndpointInDevelopment()
    {
        using HttpClient client = CreateClient();

        HttpResponseMessage uiResponse = await client.GetAsync("/swagger/index.html");
        HttpResponseMessage documentResponse = await client.GetAsync("/swagger/v1/swagger.json");
        string document = await documentResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, uiResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, documentResponse.StatusCode);
        Assert.Contains("/api/auth/register", document, StringComparison.Ordinal);
    }

    private HttpClient CreateClient()
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    private async Task<int> CountUsersAsync()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context =
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await context.Set<User>().CountAsync();
    }

    private static RegisterUserRequest ValidRequest() =>
        new("user@example.com", "ali.dev", PlaintextPassword);
}
