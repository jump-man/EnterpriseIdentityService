using EnterpriseIdentityService.Application.Abstractions;
namespace EnterpriseIdentityService.Application.Authentication.LogoutAll;
public static class LogoutAllErrors
{
    public static readonly Error InvalidAuthentication = new("LogoutAll.InvalidAuthentication", "Authentication is no longer valid.");
    public static readonly Error Conflict = new("LogoutAll.Conflict", "Authentication state changed during the request.");
}
