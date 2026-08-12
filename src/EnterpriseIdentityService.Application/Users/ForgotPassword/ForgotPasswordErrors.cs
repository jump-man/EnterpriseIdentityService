using EnterpriseIdentityService.Application.Abstractions;

namespace EnterpriseIdentityService.Application.Users.ForgotPassword;

public static class ForgotPasswordErrors
{
    public static readonly Error EmailRequired = new("ForgotPassword.EmailRequired", "Email is required.");
    public static readonly Error InvalidEmail = new("ForgotPassword.InvalidEmail", "Email is invalid.");
}
