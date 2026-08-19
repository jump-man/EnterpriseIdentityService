using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EnterpriseIdentityService.Api.Endpoints.Authentication;
using EnterpriseIdentityService.Application.Authorization;
using EnterpriseIdentityService.Domain.Auditing;
using EnterpriseIdentityService.Domain.Roles;
using EnterpriseIdentityService.Domain.Users;
using EnterpriseIdentityService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.RateLimiting;

namespace EnterpriseIdentityService.IntegrationTests.Api;

public sealed class AuditWorkflowTests(ApiWebApplicationFactory factory)
    : IClassFixture<ApiWebApplicationFactory>
{
    private const string Password = "StrongPassword123!";

    [Fact]
    public async Task AuditEndpoint_ShouldReturn401Then403ThenAuthorizedHistory()
    {
        await factory.ResetDatabaseAsync();
        using HttpClient client = CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/audit")).StatusCode);

        await CreateActiveUserAsync(client, "member@example.com", "member.audit", false);
        LoginResponse memberLogin = await LoginAsync(client, "member@example.com", Password);
        using HttpClient member = ClientWithToken(memberLogin.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await member.GetAsync("/api/audit")).StatusCode);

        await CreateActiveUserAsync(client, "admin@example.com", "admin.audit", true);
        LoginResponse adminLogin = await LoginAsync(client, "admin@example.com", Password);
        using HttpClient admin = ClientWithToken(adminLogin.AccessToken);
        HttpResponseMessage response = await admin.GetAsync("/api/audit");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement[] items = await ReadItemsAsync(response);
        Assert.Contains(items, item =>
            item.GetProperty("eventType").GetString() == nameof(AuditEventType.LoginSucceeded));
        Assert.Contains(items, item =>
            item.GetProperty("eventType").GetString() == nameof(AuditEventType.SessionCreated));
    }

    [Fact]
    public async Task FailedLoginAudit_ShouldPreserveAntiEnumerationAndExcludeSensitiveInputs()
    {
        await factory.ResetDatabaseAsync();
        using HttpClient client = CreateClient();
        const string sensitivePassword = "Never-Audit-This-Password!";
        const string unknownEmail = "does-not-exist@example.com";
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Audit-Test-Agent/1.0");

        HttpResponseMessage failed = await client.PostAsJsonAsync(
            "/api/auth/login", new { email = unknownEmail, password = sensitivePassword });
        Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);

        await CreateActiveUserAsync(client, "admin@example.com", "admin.audit", true);
        LoginResponse login = await LoginAsync(client, "admin@example.com", Password);
        using HttpClient admin = ClientWithToken(login.AccessToken);
        HttpResponseMessage response = await admin.GetAsync(
            $"/api/audit?eventType={nameof(AuditEventType.LoginFailed)}");
        JsonElement failedEntry = Assert.Single(await ReadItemsAsync(response));
        string serialized = failedEntry.GetRawText();

        Assert.Equal(nameof(AuditOutcome.Failure),
            failedEntry.GetProperty("outcome").GetString());
        Assert.Equal(nameof(AuditReasonCode.InvalidCredentials),
            failedEntry.GetProperty("reasonCode").GetString());
        Assert.Equal(JsonValueKind.Null, failedEntry.GetProperty("targetUserId").ValueKind);
        Assert.Equal("Audit-Test-Agent/1.0", failedEntry.GetProperty("userAgent").GetString());
        Assert.DoesNotContain(sensitivePassword, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(unknownEmail, serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("authorization", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RoleAdministrationAudits_ShouldSupportCombinedRoleUserAndEventFilters()
    {
        await factory.ResetDatabaseAsync();
        using HttpClient client = CreateClient();
        Guid adminId = await CreateActiveUserAsync(client, "admin@example.com", "admin.audit", true);
        Guid targetId = await CreateActiveUserAsync(client, "target@example.com", "target.audit", false);
        LoginResponse login = await LoginAsync(client, "admin@example.com", Password);
        using HttpClient admin = ClientWithToken(login.AccessToken);

        Guid roleId = await CreateRoleAsync(admin, "Audited Operators");
        (await admin.PutAsJsonAsync($"/api/roles/{roleId}/permissions",
            new { permissions = new[] { Permissions.Roles.Read } })).EnsureSuccessStatusCode();
        (await admin.PostAsync($"/api/users/{targetId}/roles/{roleId}", null))
            .EnsureSuccessStatusCode();
        (await admin.DeleteAsync($"/api/users/{targetId}/roles/{roleId}"))
            .EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/roles/{roleId}/name",
            new { name = "Renamed Audited Operators" })).EnsureSuccessStatusCode();
        (await admin.PostAsync($"/api/roles/{roleId}/disable", null)).EnsureSuccessStatusCode();
        (await admin.PostAsync($"/api/roles/{roleId}/enable", null)).EnsureSuccessStatusCode();
        (await admin.DeleteAsync($"/api/roles/{roleId}")).EnsureSuccessStatusCode();

        string from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddMinutes(-5).ToString("O"));
        string to = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddMinutes(5).ToString("O"));
        HttpResponseMessage roleResponse = await admin.GetAsync(
            $"/api/audit?roleId={roleId}&eventType={nameof(AuditEventType.PermissionGrantedToRole)}&from={from}&to={to}");
        JsonElement permission = Assert.Single(await ReadItemsAsync(roleResponse));
        Assert.Equal(roleId, permission.GetProperty("roleId").GetGuid());
        Assert.Equal(Permissions.Roles.Read, permission.GetProperty("permission").GetString());
        Assert.Equal(adminId, permission.GetProperty("actorUserId").GetGuid());

        JsonElement[] roleItems = await ReadItemsAsync(await admin.GetAsync(
            $"/api/audit?roleId={roleId}"));
        string?[] roleEvents = roleItems
            .Select(item => item.GetProperty("eventType").GetString()).ToArray();
        Assert.Contains(nameof(AuditEventType.RoleCreated), roleEvents);
        Assert.Contains(nameof(AuditEventType.RoleRenamed), roleEvents);
        Assert.Contains(nameof(AuditEventType.RoleDisabled), roleEvents);
        Assert.Contains(nameof(AuditEventType.RoleEnabled), roleEvents);
        Assert.Contains(nameof(AuditEventType.RoleDeleted), roleEvents);
        Assert.DoesNotContain(login.AccessToken,
            string.Join('|', roleItems.Select(item => item.GetRawText())),
            StringComparison.Ordinal);
        Assert.DoesNotContain("authorization", string.Join('|', roleItems.Select(item => item.GetRawText())),
            StringComparison.OrdinalIgnoreCase);

        HttpResponseMessage userResponse = await admin.GetAsync($"/api/audit?userId={targetId}");
        JsonElement[] userItems = await ReadItemsAsync(userResponse);
        Assert.Contains(userItems, item =>
            item.GetProperty("eventType").GetString() == nameof(AuditEventType.RoleAssignedToUser));
        Assert.Contains(userItems, item =>
            item.GetProperty("eventType").GetString() == nameof(AuditEventType.RoleRemovedFromUser));

        string correlationId = permission.GetProperty("correlationId").GetString()!;
        JsonElement correlated = Assert.Single(await ReadItemsAsync(await admin.GetAsync(
            $"/api/audit?correlationId={Uri.EscapeDataString(correlationId)}")));
        Assert.Equal(permission.GetProperty("id").GetGuid(), correlated.GetProperty("id").GetGuid());
    }

    [Fact]
    public void AuditEndpoint_ShouldUseDedicatedReadRateLimitPolicy()
    {
        EndpointDataSource source = factory.Services.GetRequiredService<EndpointDataSource>();
        RouteEndpoint endpoint = source.Endpoints.OfType<RouteEndpoint>()
            .Single(item => item.RoutePattern.RawText == "/api/audit");
        var metadata = endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>();

        Assert.NotNull(metadata);
        Assert.Equal("audit-read", metadata.PolicyName);
    }

    [Fact]
    public async Task AuditEndpoint_ShouldProvideDeterministicCursorAndRejectInvalidQueries()
    {
        await factory.ResetDatabaseAsync();
        using HttpClient client = CreateClient();
        await CreateActiveUserAsync(client, "admin@example.com", "admin.audit", true);
        LoginResponse login = await LoginAsync(client, "admin@example.com", Password);
        using HttpClient admin = ClientWithToken(login.AccessToken);
        await CreateRoleAsync(admin, "First Audit Role");
        await CreateRoleAsync(admin, "Second Audit Role");
        await CreateRoleAsync(admin, "Third Audit Role");

        HttpResponseMessage firstResponse = await admin.GetAsync(
            $"/api/audit?eventType={nameof(AuditEventType.RoleCreated)}&pageSize=2");
        using JsonDocument firstDocument = JsonDocument.Parse(
            await firstResponse.Content.ReadAsStringAsync());
        JsonElement firstRoot = firstDocument.RootElement;
        Guid[] firstIds = firstRoot.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid()).ToArray();
        string cursor = firstRoot.GetProperty("nextCursor").GetString()!;
        Assert.Equal(2, firstIds.Length);

        HttpResponseMessage secondResponse = await admin.GetAsync(
            $"/api/audit?eventType={nameof(AuditEventType.RoleCreated)}&pageSize=2&cursor={Uri.EscapeDataString(cursor)}");
        JsonElement[] secondItems = await ReadItemsAsync(secondResponse);
        Assert.Single(secondItems);
        Assert.DoesNotContain(secondItems[0].GetProperty("id").GetGuid(), firstIds);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.GetAsync("/api/audit?pageSize=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.GetAsync("/api/audit?eventType=AnythingGoes")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.GetAsync("/api/audit?cursor=not-a-cursor")).StatusCode);
        string invalidFrom = Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"));
        string invalidTo = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.GetAsync($"/api/audit?from={invalidFrom}&to={invalidTo}")).StatusCode);
    }

    private HttpClient CreateClient() => factory.CreateClient(
        new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    private HttpClient ClientWithToken(string accessToken)
    {
        HttpClient client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private async Task<Guid> CreateActiveUserAsync(
        HttpClient client, string email, string username, bool administrator)
    {
        (await client.PostAsJsonAsync(
            "/api/auth/register", new { email, username, password = Password }))
            .EnsureSuccessStatusCode();
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        User user = await context.Set<User>().SingleAsync(item => item.Email == Email.Create(email));
        user.VerifyEmail(DateTimeOffset.UtcNow);
        if (administrator)
        {
            context.Set<UserRole>().Add(UserRole.Create(user.Id, BuiltInRoles.AdministratorId));
            user.InvalidateAuthorization();
        }
        await context.SaveChangesAsync();
        return user.Id.Value;
    }

    private static async Task<LoginResponse> LoginAsync(
        HttpClient client, string email, string password)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
    }

    private static async Task<Guid> CreateRoleAsync(HttpClient admin, string name)
    {
        HttpResponseMessage response = await admin.PostAsJsonAsync("/api/roles", new { name });
        response.EnsureSuccessStatusCode();
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement[]> ReadItemsAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.Clone()).ToArray();
    }
}
