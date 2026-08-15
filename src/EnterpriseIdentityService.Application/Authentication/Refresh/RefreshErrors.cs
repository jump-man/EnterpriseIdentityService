using EnterpriseIdentityService.Application.Abstractions;
namespace EnterpriseIdentityService.Application.Authentication.Refresh;
public static class RefreshErrors
{
    public static readonly Error TokenRequired = new("Refresh.TokenRequired", "Refresh token is required.");
    public static readonly Error InvalidToken = new("Refresh.InvalidToken", "The refresh credential is invalid.");
}
