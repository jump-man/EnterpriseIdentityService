using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace EnterpriseIdentityService.IntegrationTests.Api;

public sealed class OperationalReadinessTests(ApiWebApplicationFactory factory)
    : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task HealthEndpoints_ShouldBeAnonymousMinimalAndDatabaseReady()
    {
        await factory.ResetDatabaseAsync();
        using HttpClient client = CreateClient(factory);

        HttpResponseMessage live = await client.GetAsync("/health/live");
        HttpResponseMessage ready = await client.GetAsync("/health/ready");
        HttpResponseMessage obsolete = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, obsolete.StatusCode);
        await AssertMinimalHealthBodyAsync(live, "Healthy");
        await AssertMinimalHealthBodyAsync(ready, "Healthy");
    }

    [Fact]
    public async Task DependencyOutage_ShouldFailReadinessWithoutFailingLiveness()
    {
        using WebApplicationFactory<Program> unavailable = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.Configure<HealthCheckServiceOptions>(options =>
                    options.Registrations.Add(new HealthCheckRegistration(
                        "simulated-database-outage",
                        _ => new UnhealthyCheck(),
                        HealthStatus.Unhealthy,
                        ["ready"])))));
        using HttpClient client = CreateClient(unavailable);

        HttpResponseMessage live = await client.GetAsync("/health/live");
        HttpResponseMessage ready = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        await AssertMinimalHealthBodyAsync(live, "Healthy");
        string readyBody = await AssertMinimalHealthBodyAsync(ready, "Unhealthy");
        Assert.DoesNotContain("simulated-database-outage", readyBody, StringComparison.Ordinal);
        Assert.DoesNotContain("exception", readyBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Server=", readyBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HealthEndpoints_ShouldExplicitlyDisableAuthorizationAndRateLimiting()
    {
        EndpointDataSource source = factory.Services.GetRequiredService<EndpointDataSource>();

        foreach (string route in new[] { "/health/live", "/health/ready" })
        {
            RouteEndpoint endpoint = source.Endpoints.OfType<RouteEndpoint>()
                .Single(item => item.RoutePattern.RawText == route);

            Assert.NotNull(endpoint.Metadata.GetMetadata<IAllowAnonymous>());
            Assert.NotNull(endpoint.Metadata.GetMetadata<DisableRateLimitingAttribute>());
            Assert.Null(endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>());
        }
    }

    [Fact]
    public void OpenTelemetryFoundation_ShouldRegisterTracingAndMetricsProviders()
    {
        Assert.NotNull(factory.Services.GetService<TracerProvider>());
        Assert.NotNull(factory.Services.GetService<MeterProvider>());
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> application) =>
        application.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

    private static async Task<string> AssertMinimalHealthBodyAsync(
        HttpResponseMessage response,
        string expectedStatus)
    {
        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument document = JsonDocument.Parse(body);

        JsonProperty property = Assert.Single(document.RootElement.EnumerateObject());
        Assert.Equal("status", property.Name);
        Assert.Equal(expectedStatus, property.Value.GetString());
        return body;
    }

    private sealed class UnhealthyCheck : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(HealthCheckResult.Unhealthy("sensitive dependency detail"));
    }
}
