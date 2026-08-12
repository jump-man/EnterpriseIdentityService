using EnterpriseIdentityService.Application.Abstractions;

namespace EnterpriseIdentityService.Application.Users.ResetPassword;

public static class ResetPasswordErrors
{
    public static readonly Error TokenRequired = new("ResetPassword.TokenRequired", "Reset token is required.");
    public static readonly Error PasswordRequired = new("ResetPassword.PasswordRequired", "New password is required.");
    public static readonly Error InvalidToken = new("ResetPassword.InvalidToken", "The reset token is invalid or expired.");
}
