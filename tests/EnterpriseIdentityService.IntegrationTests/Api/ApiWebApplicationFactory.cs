using EnterpriseIdentityService.Infrastructure.Persistence;
using EnterpriseIdentityService.Application.Abstractions.Mailing;
using EnterpriseIdentityService.IntegrationTests.TestDoubles;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace EnterpriseIdentityService.IntegrationTests.Api;

public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();

        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureAppConfiguration(configurationBuilder =>
            configurationBuilder.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Database"] =
                        "Server=unused;Database=unused;Trusted_Connection=True",
                    ["Jwt:Issuer"] = "EnterpriseIdentityService.Tests",
                    ["Jwt:Audience"] = "EnterpriseIdentityService.Tests.Client",
                    ["Jwt:SigningKey"] = "test-only-signing-key-with-at-least-thirty-two-characters",
                    ["Jwt:ExpirationMinutes"] = "15",
                    ["EmailVerification:TokenLifetime"] = "1.00:00:00",
                    ["EmailVerification:ResendCooldown"] = "00:01:00",
                    ["EmailVerification:PublicBaseUrl"] = "https://localhost",
                    ["Resend:Enabled"] = "false"
                }));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(_connection));
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<RecordingEmailSender>();
            services.AddSingleton<IEmailSender>(provider =>
                provider.GetRequiredService<RecordingEmailSender>());
        });
    }

    public async Task ResetDatabaseAsync()
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context =
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        Services.GetRequiredService<RecordingEmailSender>().Clear();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
