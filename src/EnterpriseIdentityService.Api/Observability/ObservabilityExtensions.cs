using System.Reflection;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace EnterpriseIdentityService.Api.Observability;

internal static class ObservabilityExtensions
{
    public static IServiceCollection AddOperationalObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                string? correlationId = CorrelationId.GetCurrent(context.HttpContext);
                if (correlationId is not null)
                {
                    context.ProblemDetails.Extensions["correlationId"] = correlationId;
                }
            };
        });
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.Configure<ExceptionHandlerOptions>(options =>
            options.SuppressDiagnosticsCallback = _ => true);

        Assembly assembly = typeof(Program).Assembly;
        string serviceName = assembly.GetName().Name ?? "EnterpriseIdentityService.Api";
        string? serviceVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        OpenTelemetryBuilder openTelemetry = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName, serviceVersion: serviceVersion)
                .AddAttributes([
                    new KeyValuePair<string, object>(
                        "deployment.environment.name",
                        environment.EnvironmentName)
                ]))
            .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation());

        if (!string.IsNullOrWhiteSpace(
            configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        {
            openTelemetry.UseAzureMonitor();
        }

        return services;
    }
}
