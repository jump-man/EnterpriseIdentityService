using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EnterpriseIdentityService.Api.Endpoints.Authentication;
using EnterpriseIdentityService.Application.Authorization;
using EnterpriseIdentityService.Domain.Roles;
using EnterpriseIdentityService.Domain.Users;
using EnterpriseIdentityService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EnterpriseIdentityService.Domain.Auditing;

namespace EnterpriseIdentityService.IntegrationTests.Api;

public sealed class AuthorizationWorkflowTests(ApiWebApplicationFactory factory)
    : IClassFixture<ApiWebApplicationFactory>
{
    private const string Password = "StrongPassword123!";

    [Fact]
    public async Task RolesEndpoint_ShouldReturn401Then403ThenSuccess()
    {
        await factory.ResetDatabaseAsync();
        using HttpClient anonymous = CreateClient();

        HttpResponseMessage unauthenticated = await anonymous.GetAsync("/api/roles");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        Assert.Equal("application/problem+json",
            unauthenticated.Content.Headers.ContentType?.MediaType);

        await CreateActiveUserAsync(anonymous, "member@example.com", "member.user", false);
        LoginResponse memberLogin = await LoginAsync(anonymous, "member@example.com");
        using HttpClient member = ClientWithToken(memberLogin.AccessToken);
        HttpResponseMessage forbidden = await member.GetAsync("/api/roles");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal("application/problem+json", forbidden.Content.Headers.ContentType?.MediaType);

        await CreateActiveUserAsync(anonymous, "admin@example.com", "admin.user", true);
        LoginResponse adminLogin = await LoginAsync(anonymous, "admin@example.com");
        using HttpClient admin = ClientWithToken(adminLogin.AccessToken);
        HttpResponseMessage allowed = await admin.GetAsync("/api/roles");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        string body = await allowed.Content.ReadAsStringAsync();
        Assert.Contains(BuiltInRoles.AdministratorName, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AssignAndRemoveRole_ShouldInvalidateAccessButPreserveRefreshSession()
    {
        await factory.ResetDatabaseAsync();
        using HttpClient client = CreateClient();
        Guid adminId = await CreateActiveUserAsync(
            client, "admin@example.com", "admin.user", true);
        Guid memberId = await CreateActiveUserAsync(
            client, "member@example.com", "member.user", false);
        LoginResponse adminLogin = await LoginAsync(client, "admin@example.com");
        LoginResponse memberLogin = await LoginAsync(client, "member@example.com");
        using HttpClient admin = ClientWithToken(adminLogin.AccessToken);

        Guid roleId = await CreateRoleAsync(admin, "Role Readers");
        await ReplacePermissionsAsync(admin, roleId, [Permissions.Roles.Read]);

        HttpResponseMessage assigned = await admin.PostAsync(
            $"/api/users/{memberId}/roles/{roleId}", null);
        Assert.Equal(HttpStatusCode.NoContent, assigned.StatusCode);

        using HttpClient staleMember = ClientWithToken(memberLogin.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await staleMember.GetAsync("/api/roles")).StatusCode);

        LoginResponse refreshed = await RefreshAsync(client, memberLogin.RefreshToken);
        using HttpClient authorizedMember = ClientWithToken(refreshed.AccessToken);
        Assert.Equal(HttpStatusCode.OK,
            (await authorizedMember.GetAsync("/api/roles")).StatusCode);

        HttpResponseMessage removed = await admin.DeleteAsync(
            $"/api/users/{memberId}/roles/{roleId}");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await authorizedMember.GetAsync("/api/roles")).StatusCode);

        LoginResponse afterRemoval = await RefreshAsync(client, refreshed.RefreshToken);
        using HttpClient currentMember = ClientWithToken(afterRemoval.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await currentMember.GetAsync("/api/roles")).StatusCode);

        await AssertAuthorizationVersionOnlyChangedAsync(memberId, expectedVersion: 2);
        Assert.NotEqual(Guid.Empty, adminId);
    }

    [Fact]
    public async Task PermissionAndRoleStateChanges_ShouldRefreshCurrentEffectivePermissions()
    {
        await factory.ResetDatabaseAsync();
        using HttpClient client = CreateClient();
        await CreateActiveUserAsync(client, "admin@example.com", "admin.user", true);
        Guid memberId = await CreateActiveUserAsync(client, "member@example.com", "member.user", false);
        LoginResponse adminLogin = await LoginAsync(client, "admin@example.com");
        using HttpClient admin = ClientWithToken(adminLogin.AccessToken);
        Guid roleId = await CreateRoleAsync(admin, "Operators");
        await ReplacePermissionsAsync(admin, roleId, [Permissions.Roles.Read]);
        Assert.Equal(HttpStatusCode.NoContent,
            (await admin.PostAsync($"/api/users/{memberId}/roles/{roleId}", null)).StatusCode);

        LoginResponse member = await LoginAsync(client, "member@example.com");
        using HttpClient memberClient = ClientWithToken(member.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await memberClient.GetAsync("/api/roles")).StatusCode);

        await ReplacePermissionsAsync(admin, roleId, []);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await memberClient.GetAsync("/api/roles")).StatusCode);
        member = await RefreshAsync(client, member.RefreshToken);
        using HttpClient withoutPermission = ClientWithToken(member.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await withoutPermission.GetAsync("/api/roles")).StatusCode);

        await ReplacePermissionsAsync(admin, roleId, [Permissions.Roles.Read]);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await withoutPermission.GetAsync("/api/roles")).StatusCode);
        member = await RefreshAsync(client, member.RefreshToken);
        using HttpClient restored = ClientWithToken(member.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await restored.GetAsync("/api/roles")).StatusCode);

        Assert.Equal(HttpStatusCode.OK,
            (await admin.PostAsync($"/api/roles/{roleId}/disable", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await restored.GetAsync("/api/roles")).StatusCode);
        member = await RefreshAsync(client, member.RefreshToken);
        using HttpClient disabled = ClientWithToken(member.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await disabled.GetAsync("/api/roles")).StatusCode);

        Assert.Equal(HttpStatusCode.OK,
            (await admin.PostAsync($"/api/roles/{roleId}/enable", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await disabled.GetAsync("/api/roles")).StatusCode);
        member = await RefreshAsync(client, member.RefreshToken);
        using HttpClient reenabled = ClientWithToken(member.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await reenabled.GetAsync("/api/roles")).StatusCode);
    }

    [Fact]
    public async Task GrantCeilingAndSystemRoleProtections_ShouldBeEnforced()
    {
        await factory.ResetDatabaseAsync();
        using HttpClient client = CreateClient();
        Guid adminId = await CreateActiveUserAsync(client, "admin@example.com", "admin.user", true);
        Guid limitedId = await CreateActiveUserAsync(client, "limited@example.com", "limited.user", false);
        Guid targetId = await CreateActiveUserAsync(client, "target@example.com", "target.user", false);
        LoginResponse adminLogin = await LoginAsync(client, "admin@example.com");
        using HttpClient admin = ClientWithToken(adminLogin.AccessToken);

        Guid limitedRoleId = await CreateRoleAsync(admin, "Limited Role Managers");
        await ReplacePermissionsAsync(admin, limitedRoleId,
            [Permissions.Roles.Manage, Permissions.UserRoles.Manage]);
        Assert.Equal(HttpStatusCode.NoContent,
            (await admin.PostAsync($"/api/users/{limitedId}/roles/{limitedRoleId}", null)).StatusCode);
        Guid privilegedRoleId = await CreateRoleAsync(admin, "Role Readers");
        await ReplacePermissionsAsync(admin, privilegedRoleId, [Permissions.Roles.Read]);

        LoginResponse limitedLogin = await LoginAsync(client, "limited@example.com");
        using HttpClient limited = ClientWithToken(limitedLogin.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await limited.PostAsync($"/api/users/{targetId}/roles/{privilegedRoleId}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await limited.PutAsJsonAsync($"/api/roles/{privilegedRoleId}/permissions",
                new { permissions = new[] { Permissions.Roles.Read } })).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PutAsJsonAsync($"/api/roles/{privilegedRoleId}/permissions",
                new { permissions = new[] { "whatever.admin" } })).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await admin.DeleteAsync($"/api/roles/{BuiltInRoles.AdministratorId.Value}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await admin.DeleteAsync(
                $"/api/users/{adminId}/roles/{BuiltInRoles.AdministratorId.Value}")).StatusCode);

        await using AsyncServiceScope auditScope = factory.Services.CreateAsyncScope();
        ApplicationDbContext auditContext =
            auditScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        AuditReasonCode?[] reasons = await auditContext.Set<AuditEntry>()
            .Where(entry => entry.EventType == AuditEventType.AuthorizationChangeRejected)
            .Select(entry => entry.ReasonCode)
            .ToArrayAsync();
        Assert.Contains(AuditReasonCode.GrantCeilingViolation, reasons);
        Assert.Contains(AuditReasonCode.ProtectedSystemRole, reasons);
        Assert.Contains(AuditReasonCode.LastAdministratorProtection, reasons);
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
        HttpClient client,
        string email,
        string username,
        bool administrator)
    {
        HttpResponseMessage registered = await client.PostAsJsonAsync(
            "/api/auth/register", new { email, username, password = Password });
        registered.EnsureSuccessStatusCode();

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

    private static async Task<LoginResponse> LoginAsync(HttpClient client, string email)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
    }

    private static async Task<LoginResponse> RefreshAsync(HttpClient client, string refreshToken)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/refresh", new { refreshToken });
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

    private static async Task ReplacePermissionsAsync(
        HttpClient admin,
        Guid roleId,
        IReadOnlyCollection<string> permissions)
    {
        HttpResponseMessage response = await admin.PutAsJsonAsync(
            $"/api/roles/{roleId}/permissions", new { permissions });
        response.EnsureSuccessStatusCode();
    }

    private async Task AssertAuthorizationVersionOnlyChangedAsync(Guid userId, int expectedVersion)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        User user = await context.Set<User>().SingleAsync(item => item.Id == new UserId(userId));
        Assert.Equal(expectedVersion, user.AuthorizationVersion);
        Assert.Equal(0, user.TokenVersion);
    }
}
