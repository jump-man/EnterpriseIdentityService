using EnterpriseIdentityService.Application.Abstractions;

namespace EnterpriseIdentityService.Application.Users.GetCurrentUser;

public static class GetCurrentUserErrors
{
    public static readonly Error NotFound = new(
        "Users.Current.NotFound", "The authenticated user no longer exists.");
}
