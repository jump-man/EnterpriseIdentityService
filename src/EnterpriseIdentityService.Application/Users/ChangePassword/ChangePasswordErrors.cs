using EnterpriseIdentityService.Application.Abstractions;

namespace EnterpriseIdentityService.Application.Users.ChangePassword;

public static class ChangePasswordErrors
{
    public static readonly Error UserNotFound = new("ChangePassword.UserNotFound", "The authenticated user is no longer available.");
    public static readonly Error Forbidden = new("ChangePassword.Forbidden", "The account is not eligible to change its password.");
    public static readonly Error CurrentPasswordRequired = new("ChangePassword.CurrentPasswordRequired", "Current password is required.");
    public static readonly Error NewPasswordRequired = new("ChangePassword.NewPasswordRequired", "New password is required.");
    public static readonly Error InvalidCurrentPassword = new("ChangePassword.InvalidCurrentPassword", "The current password is incorrect.");
    public static readonly Error SamePassword = new("ChangePassword.SamePassword", "The new password must differ from the current password.");
    public static readonly Error ConcurrencyConflict = new("ChangePassword.ConcurrencyConflict", "The account security state changed during the request. Try again.");
}
