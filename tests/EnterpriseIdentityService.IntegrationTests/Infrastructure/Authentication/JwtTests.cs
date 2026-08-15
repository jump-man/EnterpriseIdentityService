using System.IdentityModel.Tokens.Jwt;
using System.Text;
using EnterpriseIdentityService.Domain.Users;
using EnterpriseIdentityService.Infrastructure.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EnterpriseIdentityService.IntegrationTests.Infrastructure.Authentication;

public sealed class JwtTests
{
    private const string SigningKey =
        "test-only-signing-key-with-at-least-thirty-two-characters";

    [Fact]
    public void Generate_ShouldCreateValidFiniteTokenWithRequiredClaims()
    {
        DateTimeOffset now = new(2026, 8, 5, 10, 0, 0, TimeSpan.Zero);
        JwtOptions options = ValidOptions();
        var provider = new JwtAccessTokenProvider(
            Options.Create(options), new FixedTimeProvider(now));
        User user = ActiveUser();
        UserSessionId sessionId = UserSessionId.New();

        var result = provider.Generate(user, sessionId);
        var handler = new JwtSecurityTokenHandler();
        TokenValidationParameters validationParameters = ValidationParameters(options);
        validationParameters.LifetimeValidator = (notBefore, expires, _, _) =>
            notBefore <= now.UtcDateTime && expires > now.UtcDateTime;
        handler.ValidateToken(result.Value, validationParameters, out _);
        JwtSecurityToken token = handler.ReadJwtToken(result.Value);

        Assert.Equal(now.AddMinutes(15), result.ExpiresAtUtc);
        Assert.Equal(user.Id.Value.ToString(), token.Subject);
        Assert.False(string.IsNullOrWhiteSpace(token.Id));
        Assert.Equal(user.TokenVersion.ToString(), token.Claims.Single(x => x.Type == "token_version").Value);
        Assert.Equal(sessionId.Value.ToString(), token.Claims.Single(x => x.Type == "sid").Value);
        Assert.Equal(SecurityAlgorithms.HmacSha256, token.SignatureAlgorithm);
        Assert.DoesNotContain(token.Claims, claim =>
            claim.Type.Contains("password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GeneratedToken_ShouldFailValidationWithWrongKeyIssuerOrAudience()
    {
        JwtOptions options = ValidOptions();
        var provider = new JwtAccessTokenProvider(
            Options.Create(options), TimeProvider.System);
        string token = provider.Generate(ActiveUser(), UserSessionId.New()).Value;
        var handler = new JwtSecurityTokenHandler();

        TokenValidationParameters wrongKey = ValidationParameters(options);
        wrongKey.IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("another-test-only-signing-key-that-is-long-enough-value"));
        Assert.ThrowsAny<SecurityTokenException>(() =>
            handler.ValidateToken(token, wrongKey, out _));

        TokenValidationParameters wrongIssuer = ValidationParameters(options);
        wrongIssuer.ValidIssuer = "wrong-issuer";
        Assert.ThrowsAny<SecurityTokenException>(() =>
            handler.ValidateToken(token, wrongIssuer, out _));

        TokenValidationParameters wrongAudience = ValidationParameters(options);
        wrongAudience.ValidAudience = "wrong-audience";
        Assert.ThrowsAny<SecurityTokenException>(() =>
            handler.ValidateToken(token, wrongAudience, out _));
    }

    [Fact]
    public void OptionsValidator_ShouldAcceptValidOptions()
    {
        Assert.True(new JwtOptionsValidator().Validate(null, ValidOptions()).Succeeded);
    }

    [Theory]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("key")]
    [InlineData("expiration")]
    public void OptionsValidator_ShouldRejectInvalidOptions(string invalidProperty)
    {
        JwtOptions valid = ValidOptions();
        JwtOptions invalid = invalidProperty switch
        {
            "issuer" => new JwtOptions
            {
                Audience = valid.Audience,
                SigningKey = valid.SigningKey,
                ExpirationMinutes = valid.ExpirationMinutes
            },
            "audience" => new JwtOptions
            {
                Issuer = valid.Issuer,
                SigningKey = valid.SigningKey,
                ExpirationMinutes = valid.ExpirationMinutes
            },
            "key" => new JwtOptions
            {
                Issuer = valid.Issuer,
                Audience = valid.Audience,
                SigningKey = "short",
                ExpirationMinutes = valid.ExpirationMinutes
            },
            _ => new JwtOptions
            {
                Issuer = valid.Issuer,
                Audience = valid.Audience,
                SigningKey = valid.SigningKey,
                ExpirationMinutes = 0
            }
        };

        Assert.True(new JwtOptionsValidator().Validate(null, invalid).Failed);
    }

    private static JwtOptions ValidOptions() => new()
    {
        Issuer = "EnterpriseIdentityService.Tests",
        Audience = "EnterpriseIdentityService.Tests.Client",
        SigningKey = SigningKey,
        ExpirationMinutes = 15
    };

    private static TokenValidationParameters ValidationParameters(JwtOptions options) => new()
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
        ValidateIssuer = true,
        ValidIssuer = options.Issuer,
        ValidateAudience = true,
        ValidAudience = options.Audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    private static User ActiveUser()
    {
        User user = User.Register(
            UserId.New(), Email.Create("user@example.com"), Username.Create("ali.dev"),
            PasswordHash.Create("stored-hash"), DateTimeOffset.UtcNow);
        user.VerifyEmail(DateTimeOffset.UtcNow);
        return user;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
