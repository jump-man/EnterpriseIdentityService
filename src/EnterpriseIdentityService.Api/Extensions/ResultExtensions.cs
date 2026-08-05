using EnterpriseIdentityService.Application.Abstractions;
using EnterpriseIdentityService.Application.Users.Register;
using EnterpriseIdentityService.Application.Authentication.Login;
using EnterpriseIdentityService.Application.Users.GetCurrentUser;

namespace EnterpriseIdentityService.Api.Extensions;

internal static class ResultExtensions
{
    public static IResult ToProblem(this Result result)
    {
        int statusCode = result.Error == LoginErrors.InvalidCredentials
            ? StatusCodes.Status401Unauthorized
            : result.Error == GetCurrentUserErrors.NotFound
                ? StatusCodes.Status404NotFound
            : result.Error == RegisterUserErrors.EmailAlreadyInUse ||
            result.Error == RegisterUserErrors.UsernameAlreadyInUse
                ? StatusCodes.Status409Conflict
                : StatusCodes.Status400BadRequest;

        string title = statusCode switch
        {
            StatusCodes.Status401Unauthorized => "Authentication failed.",
            StatusCodes.Status404NotFound => "The requested resource was not found.",
            StatusCodes.Status409Conflict => "A registration conflict occurred.",
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
