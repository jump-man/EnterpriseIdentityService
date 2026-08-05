using EnterpriseIdentityService.Application.Abstractions;

namespace EnterpriseIdentityService.Application.Authentication.Login;

public static class LoginErrors
{
    public static readonly Error EmailRequired = new(
        "Authentication.Login.EmailRequired", "Email is required.");

    public static readonly Error PasswordRequired = new(
        "Authentication.Login.PasswordRequired", "Password is required.");

    public static readonly Error InvalidEmail = new(
        "Authentication.Login.InvalidEmail", "The supplied email address is invalid.");

    public static readonly Error InvalidCredentials = new(
        "Authentication.InvalidCredentials", "The email address or password is invalid.");
}
