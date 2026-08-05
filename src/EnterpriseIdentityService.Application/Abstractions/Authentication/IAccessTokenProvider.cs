using EnterpriseIdentityService.Domain.Users;

namespace EnterpriseIdentityService.Application.Abstractions.Authentication;

public interface IAccessTokenProvider
{
    AccessToken Generate(User user);
}
