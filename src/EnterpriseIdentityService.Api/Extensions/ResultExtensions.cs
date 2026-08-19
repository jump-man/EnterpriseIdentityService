using EnterpriseIdentityService.Application.Abstractions;
using EnterpriseIdentityService.Application.Users.Register;
using EnterpriseIdentityService.Application.Authentication.Login;
using EnterpriseIdentityService.Application.Users.GetCurrentUser;
using EnterpriseIdentityService.Application.Users.ResendVerificationEmail;
using EnterpriseIdentityService.Application.Users.ChangePassword;
using EnterpriseIdentityService.Application.Authentication.Refresh;
using EnterpriseIdentityService.Application.Authentication.LogoutAll;
using EnterpriseIdentityService.Application.Authorization;
using EnterpriseIdentityService.Application.Authentication.Logout;

namespace EnterpriseIdentityService.Api.Extensions;

internal static class ResultExtensions
{
    public static IResult ToProblem(this Result result)
    {
        int statusCode = result.Error == AuthorizationErrors.RoleNotFound ||
            result.Error == AuthorizationErrors.UserNotFound
            ? StatusCodes.Status404NotFound
            : result.Error == AuthorizationErrors.SystemRoleProtected ||
              result.Error == AuthorizationErrors.GrantCeilingExceeded ||
              result.Error == AuthorizationErrors.InvalidActor
            ? StatusCodes.Status403Forbidden
            : result.Error == AuthorizationErrors.RoleAlreadyExists ||
              result.Error == AuthorizationErrors.RoleHasAssignedUsers ||
              result.Error == AuthorizationErrors.RoleAlreadyAssigned ||
              result.Error == AuthorizationErrors.RoleNotAssigned ||
              result.Error == AuthorizationErrors.LastAdministratorRequired ||
              result.Error == AuthorizationErrors.ConcurrencyConflict
              || result.Error == LogoutErrors.Conflict
            ? StatusCodes.Status409Conflict
            : result.Error == RegisterUserErrors.EmailDeliveryUnavailable ||
            result.Error == ResendVerificationEmailErrors.EmailDeliveryUnavailable
            ? StatusCodes.Status503ServiceUnavailable
            : result.Error == LoginErrors.InvalidCredentials
            ? StatusCodes.Status401Unauthorized
            : result.Error == GetCurrentUserErrors.NotFound
                ? StatusCodes.Status404NotFound
            : result.Error == ChangePasswordErrors.UserNotFound
                ? StatusCodes.Status401Unauthorized
            : result.Error == RefreshErrors.InvalidToken || result.Error == LogoutAllErrors.InvalidAuthentication
                ? StatusCodes.Status401Unauthorized
            : result.Error == ChangePasswordErrors.Forbidden
                ? StatusCodes.Status403Forbidden
            : result.Error == ChangePasswordErrors.ConcurrencyConflict
                ? StatusCodes.Status409Conflict
            : result.Error == LogoutAllErrors.Conflict
                ? StatusCodes.Status409Conflict
            : result.Error == RegisterUserErrors.EmailAlreadyInUse ||
            result.Error == RegisterUserErrors.UsernameAlreadyInUse
                ? StatusCodes.Status409Conflict
                : StatusCodes.Status400BadRequest;

        string title = statusCode switch
        {
            StatusCodes.Status401Unauthorized => "Authentication failed.",
            StatusCodes.Status404NotFound => "The requested resource was not found.",
            StatusCodes.Status403Forbidden => "The operation is forbidden.",
            StatusCodes.Status409Conflict => "The operation conflicts with current state.",
            StatusCodes.Status503ServiceUnavailable => "Email delivery is temporarily unavailable.",
            _ => "The request is invalid."
        };

        return Results.Problem(
            statusCode: statusCode,
            title: title,
            detail: result.Error.Description,
            extensions: new Dictionary<string, object?>
            {
                ["errorCode"] = result.Error.Code
            });
    }
}
