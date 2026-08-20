using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EnterpriseIdentityService.Api.Observability;
using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Domain.Auditing;
using EnterpriseIdentityService.Domain.Users;
using EnterpriseIdentityService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EnterpriseIdentityService.IntegrationTests.Api;

public sealed class CorrelationWorkflowTests(ApiWebApplicationFactory factory)
    : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task MissingCorrelationHeader_ShouldReturnGeneratedSafeIdentifier()
    {
        using HttpClient client = CreateClient(factory);

        HttpResponseMessage response = await client.GetAsync("/health/live");

        string correlationId = Assert.Single(
            response.Headers.GetValues(CorrelationId.HeaderName));
        Assert.True(Guid.TryParseExact(correlationId, "N", out _));
    }

    [Fact]
    public async Task ValidCorrelationHeader_ShouldFlowToResponseAndProblemDetails()
    {
        using HttpClient client = CreateClient(factory);
        const string expected = "support-case_ABC-123.4";
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/users/me");
        request.Headers.Add(CorrelationId.HeaderName, expected);

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(expected, Assert.Single(
            response.Headers.GetValues(CorrelationId.HeaderName)));
        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>())!;
        Assert.Equal(
            expected,
            Assert.IsType<JsonElement>(problem.Extensions["correlationId"]).GetString());
    }

    [Fact]
    public async Task InvalidCorrelationHeaders_ShouldBeReplacedInsteadOfEchoed()
    {
        using HttpClient client = CreateClient(factory);
        string[] invalidValues =
        [
            new('a', CorrelationId.MaximumLength + 1),
            "contains spaces",
            "contains/slash"
        ];

        foreach (string invalid in invalidValues)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
            request.Headers.TryAddWithoutValidation(CorrelationId.HeaderName, invalid);

            HttpResponseMessage response = await client.SendAsync(request);
            string replacement = Assert.Single(
                response.Headers.GetValues(CorrelationId.HeaderName));

            Assert.NotEqual(invalid, replacement);
            Assert.True(Guid.TryParseExact(replacement, "N", out _));
        }

        using var ambiguous = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        ambiguous.Headers.TryAddWithoutValidation(
            CorrelationId.HeaderName,
            new[] { "first", "second" });
        HttpResponseMessage ambiguousResponse = await client.SendAsync(ambiguous);
        string ambiguousReplacement = Assert.Single(
            ambiguousResponse.Headers.GetValues(CorrelationId.HeaderName));
        Assert.NotEqual("first", ambiguousReplacement);
        Assert.NotEqual("second", ambiguousReplacement);
        Assert.True(Guid.TryParseExact(ambiguousReplacement, "N", out _));

        Assert.False(CorrelationId.TryNormalize("line\r\nbreak", out _));
        Assert.False(CorrelationId.TryNormalize("\r\notherwise-valid", out _));
        Assert.False(CorrelationId.TryNormalize("control\u0001value", out _));
    }

    [Fact]
    public async Task AuditTrail_ShouldUseTheEffectiveRequestCorrelationIdentifier()
    {
        await factory.ResetDatabaseAsync();
        using HttpClient client = CreateClient(factory);
        const string expected = "incident-2026_08_19";
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new
            {
                email = "unknown@example.com",
                password = "Never-Store-Or-Log-This!"
            })
        };
        request.Headers.Add(CorrelationId.HeaderName, expected);

        HttpResponseMessage response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context =
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        AuditEntry entry = await context.Set<AuditEntry>()
            .SingleAsync(item => item.EventType == AuditEventType.LoginFailed);
        Assert.Equal(expected, entry.CorrelationId);
    }

    [Fact]
    public async Task UnexpectedProductionException_ShouldReturnSanitizedCorrelatedProblem()
    {
        using var failing = new ProductionFaultWebApplicationFactory();
        using HttpClient client = CreateClient(failing);
        const string correlationId = "production-failure_123";
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new
            {
                email = "user@example.com",
                password = "Not-Relevant-To-The-Failure"
            })
        };
        request.Headers.Add(CorrelationId.HeaderName, correlationId);

        HttpResponseMessage response = await client.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();
        ProblemDetails problem = JsonSerializer.Deserialize<ProblemDetails>(
            body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("An unexpected error occurred.", problem.Title);
        Assert.Equal(correlationId, Assert.Single(
            response.Headers.GetValues(CorrelationId.HeaderName)));
        Assert.Equal(
            correlationId,
            Assert.IsType<JsonElement>(problem.Extensions["correlationId"]).GetString());
        Assert.DoesNotContain(ThrowingUserRepository.Secret, body, StringComparison.Ordinal);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("InvalidOperationException", body, StringComparison.Ordinal);
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> application) =>
        application.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

    private sealed class ProductionFaultWebApplicationFactory
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            foreach ((string key, string? value) in ValidConfiguration())
            {
                builder.UseSetting(key, value);
            }
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUserRepository>();
                services.AddScoped<IUserRepository, ThrowingUserRepository>();
            });
        }

        private static Dictionary<string, string?> ValidConfiguration() => new()
        {
            ["ConnectionStrings:Database"] =
                "Server=unused;Database=unused;Trusted_Connection=True",
            ["Jwt:Issuer"] = "EnterpriseIdentityService.Tests",
            ["Jwt:Audience"] = "EnterpriseIdentityService.Tests.Client",
            ["Jwt:SigningKey"] =
                "test-only-signing-key-with-at-least-thirty-two-characters",
            ["Jwt:ExpirationMinutes"] = "15",
            ["EmailVerification:TokenLifetime"] = "1.00:00:00",
            ["EmailVerification:ResendCooldown"] = "00:01:00",
            ["EmailVerification:PublicBaseUrl"] = "https://localhost",
            ["PasswordRecovery:TokenLifetime"] = "00:15:00",
            ["PasswordRecovery:RequestCooldown"] = "00:01:00",
            ["PasswordRecovery:PublicBaseUrl"] = "https://localhost",
            ["AuthenticationSessions:Lifetime"] = "30.00:00:00",
            ["Resend:Enabled"] = "false"
        };
    }

    private sealed class ThrowingUserRepository : IUserRepository
    {
        internal const string Secret = "sensitive-internal-exception-detail";

        public Task<User?> GetByEmailAsync(
            Email email,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException(Secret);

        public Task<User?> GetByIdAsync(
            UserId userId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> ExistsByEmailAsync(
            Email email,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> ExistsByUsernameAsync(
            Username username,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public void Add(User user) => throw new NotSupportedException();
    }
}
