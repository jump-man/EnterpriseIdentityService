using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EnterpriseIdentityService.Domain.Users;

namespace EnterpriseIdentityService.Api.Extensions;

internal static class ClaimsPrincipalExtensions
{
    public static bool TryGetUserId(this ClaimsPrincipal principal, out UserId userId)
    {
        string? subject = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (Guid.TryParse(subject, out Guid value) && value != Guid.Empty)
        {
            userId = new UserId(value);
            return true;
        }

        userId = default;
        return false;
    }
}
