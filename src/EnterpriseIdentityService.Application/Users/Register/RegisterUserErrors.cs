using EnterpriseIdentityService.Application.Abstractions;

namespace EnterpriseIdentityService.Application.Users.Register;

public static class RegisterUserErrors
{
    public static readonly Error EmailRequired = new(
        "Users.Register.EmailRequired",
        "Email is required.");

    public static readonly Error UsernameRequired = new(
        "Users.Register.UsernameRequired",
        "Username is required.");

    public static readonly Error PasswordRequired = new(
        "Users.Register.PasswordRequired",
        "Password is required.");

    public static readonly Error InvalidEmail = new(
        "Users.Register.InvalidEmail",
        "The supplied email address is invalid.");

    public static readonly Error InvalidUsername = new(
        "Users.Register.InvalidUsername",
        "The supplied username is invalid.");

    public static readonly Error EmailAlreadyInUse = new(
        "Users.Register.EmailAlreadyInUse",
        "A user with the supplied email address already exists.");

    public static readonly Error UsernameAlreadyInUse = new(
        "Users.Register.UsernameAlreadyInUse",
        "A user with the supplied username already exists.");

    public static readonly Error EmailDeliveryUnavailable = new(
        "Users.Register.EmailDeliveryUnavailable",
        "The account was created, but the verification email could not be delivered. Request another verification email later.");
}
