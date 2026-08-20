using EnterpriseIdentityService.Application.Authentication;
using EnterpriseIdentityService.Application.PasswordRecovery;
using EnterpriseIdentityService.Infrastructure.Authentication;
using EnterpriseIdentityService.Infrastructure.Mailing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EnterpriseIdentityService.IntegrationTests.Infrastructure.Authentication;

public sealed class CriticalOptionsValidationTests
{
    [Fact]
    public void AuthenticationSessionValidator_ShouldAcceptPositiveAndRejectNonPositiveLifetime()
    {
        var validator = new AuthenticationSessionOptionsValidator();

        Assert.True(validator.Validate(null, new AuthenticationSessionOptions
        {
            Lifetime = TimeSpan.FromDays(30)
        }).Succeeded);
        Assert.True(validator.Validate(null, new AuthenticationSessionOptions
        {
            Lifetime = TimeSpan.Zero
        }).Failed);
    }

    [Fact]
    public void PasswordRecoveryValidator_ShouldRequireValidLifetimesAndHttpsBaseUrl()
    {
        var validator = new PasswordRecoveryOptionsValidator();
        var valid = new PasswordRecoveryOptions
        {
            TokenLifetime = TimeSpan.FromMinutes(15),
            RequestCooldown = TimeSpan.FromMinutes(1),
            PublicBaseUrl = "https://identity.example"
        };

        Assert.True(validator.Validate(null, valid).Succeeded);
        Assert.True(validator.Validate(null, new PasswordRecoveryOptions
        {
            TokenLifetime = TimeSpan.Zero,
            RequestCooldown = TimeSpan.FromSeconds(-1),
            PublicBaseUrl = "http://identity.example"
        }).Failed);
    }

    [Fact]
    public void InvalidJwtConfiguration_ShouldFailHostStartupWithoutDisclosingKeyMaterial()
    {
        using var factory = new InvalidJwtWebApplicationFactory();

        Exception exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("Jwt:SigningKey", exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(
            InvalidJwtWebApplicationFactory.InvalidSigningKey,
            exception.ToString(),
            StringComparison.Ordinal);
    }

    private sealed class InvalidJwtWebApplicationFactory : WebApplicationFactory<Program>
    {
        internal const string InvalidSigningKey = "raw-key-material";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            foreach ((string key, string? value) in Configuration())
            {
                builder.UseSetting(key, value);
            }
        }

        private static Dictionary<string, string?> Configuration() => new()
        {
            ["ConnectionStrings:Database"] =
                "Server=unused;Database=unused;Trusted_Connection=True",
            ["Jwt:Issuer"] = "EnterpriseIdentityService.Tests",
            ["Jwt:Audience"] = "EnterpriseIdentityService.Tests.Client",
            ["Jwt:SigningKey"] = InvalidSigningKey,
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
}
