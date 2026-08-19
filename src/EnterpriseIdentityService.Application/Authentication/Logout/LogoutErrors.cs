using EnterpriseIdentityService.Application.Abstractions;

namespace EnterpriseIdentityService.Application.Authentication.Logout;

public static class LogoutErrors
{
    public static readonly Error Conflict = new(
        "Logout.Conflict", "The session changed while logout was being processed.");
}
