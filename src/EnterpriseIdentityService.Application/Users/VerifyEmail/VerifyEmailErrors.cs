using EnterpriseIdentityService.Application.Abstractions;

namespace EnterpriseIdentityService.Application.Users.VerifyEmail;

public static class VerifyEmailErrors
{
    public static readonly Error TokenRequired = new(
        "Users.VerifyEmail.TokenRequired", "Verification token is required.");
    public static readonly Error InvalidToken = new(
        "Users.VerifyEmail.InvalidToken", "The verification token is invalid or no longer usable.");
}
