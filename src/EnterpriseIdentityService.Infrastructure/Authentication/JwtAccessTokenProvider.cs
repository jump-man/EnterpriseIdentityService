using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EnterpriseIdentityService.Application.Abstractions.Authentication;
using EnterpriseIdentityService.Domain.Users;
using EnterpriseIdentityService.Application.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EnterpriseIdentityService.Infrastructure.Authentication;

internal sealed class JwtAccessTokenProvider(
    IOptions<JwtOptions> options,
    TimeProvider timeProvider) : IAccessTokenProvider
{
    private readonly JwtOptions _options = options.Value;

    public AccessToken Generate(
        User user,
        UserSessionId sessionId,
        AuthorizationSnapshot authorization)
    {
        ArgumentNullException.ThrowIfNull(user);

        DateTimeOffset issuedAt = timeProvider.GetUtcNow();
        DateTimeOffset expiresAt = issuedAt.AddMinutes(_options.ExpirationMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.Value.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat,
                EpochTime.GetIntDate(issuedAt.UtcDateTime).ToString(),
                ClaimValueTypes.Integer64),
            new(CustomClaimTypes.TokenVersion, user.TokenVersion.ToString(), ClaimValueTypes.Integer32),
            new(CustomClaimTypes.AuthorizationVersion,
                authorization.AuthorizationVersion.ToString(), ClaimValueTypes.Integer32),
            new(CustomClaimTypes.SessionId, sessionId.Value.ToString())
        };
        claims.AddRange(authorization.Permissions.Select(permission =>
            new Claim(CustomClaimTypes.Permission, permission)));

        var token = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            claims,
            issuedAt.UtcDateTime,
            expiresAt.UtcDateTime,
            new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new AccessToken(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt);
    }
}
