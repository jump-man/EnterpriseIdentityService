using System.Text.RegularExpressions;
using EnterpriseIdentityService.Application.Abstractions.Authentication;
using EnterpriseIdentityService.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseIdentityService.IntegrationTests.Infrastructure.Authentication;

public sealed partial class EmailVerificationTokenServicesTests
{
    [Fact]
    public void Generator_ShouldCreateDistinctUrlSafeTokensWithThirtyTwoBytesOfEntropy()
    {
        using ServiceProvider provider = CreateProvider();
        var generator = provider.GetRequiredService<IEmailVerificationTokenGenerator>();

        string first = generator.Generate();
        string second = generator.Generate();

        Assert.Matches(UrlSafeTokenPattern(), first);
        Assert.NotEqual(first, second);
        Assert.Equal(32, Convert.FromBase64String(ToBase64(first)).Length);
    }

    [Fact]
    public void Hasher_ShouldBeDeterministicCanonicalAndDifferentFromRawToken()
    {
        using ServiceProvider provider = CreateProvider();
        var hasher = provider.GetRequiredService<IEmailVerificationTokenHasher>();

        string first = hasher.Hash("raw-token");

        Assert.Equal(first, hasher.Hash("raw-token"));
        Assert.NotEqual(first, hasher.Hash("other-token"));
        Assert.NotEqual("raw-token", first);
        Assert.Matches("^[0-9a-f]{64}$", first);
    }

    private static ServiceProvider CreateProvider()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = "Server=unused;Database=unused",
                ["Jwt:Issuer"] = "tests",
                ["Jwt:Audience"] = "tests",
                ["Jwt:SigningKey"] = "test-only-signing-key-with-at-least-thirty-two-characters",
                ["Jwt:ExpirationMinutes"] = "15",
                ["EmailVerification:TokenLifetime"] = "1.00:00:00",
                ["EmailVerification:ResendCooldown"] = "00:01:00",
                ["EmailVerification:PublicBaseUrl"] = "https://localhost",
                ["Resend:Enabled"] = "false"
            }).Build();
        return new ServiceCollection().AddInfrastructure(configuration).BuildServiceProvider();
    }

    private static string ToBase64(string token)
    {
        string value = token.Replace('-', '+').Replace('_', '/');
        return value + new string('=', (4 - value.Length % 4) % 4);
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{43}$")]
    private static partial Regex UrlSafeTokenPattern();
}
