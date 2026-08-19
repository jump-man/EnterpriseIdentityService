using EnterpriseIdentityService.Domain.Users;
using EnterpriseIdentityService.Application.Authorization;

namespace EnterpriseIdentityService.Application.Abstractions.Authentication;

public interface IAccessTokenProvider
{
    AccessToken Generate(
        User user,
        UserSessionId sessionId,
        AuthorizationSnapshot authorization);
}
