using EnterpriseIdentityService.Application.Abstractions;

namespace EnterpriseIdentityService.Application.Users.ResendVerificationEmail;

public static class ResendVerificationEmailErrors
{
    public static readonly Error EmailRequired = new(
        "Users.ResendVerification.EmailRequired", "Email is required.");
    public static readonly Error InvalidEmail = new(
        "Users.ResendVerification.InvalidEmail", "The supplied email address is invalid.");
    public static readonly Error EmailDeliveryUnavailable = new(
        "Users.ResendVerification.EmailDeliveryUnavailable",
        "The verification email could not be delivered. Try again later.");
}
