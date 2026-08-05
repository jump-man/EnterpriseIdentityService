using EnterpriseIdentityService.Application.Abstractions;
using EnterpriseIdentityService.Application.Users.Register;

namespace EnterpriseIdentityService.Api.Extensions;

internal static class ResultExtensions
{
    public static IResult ToProblem(this Result result)
    {
        int statusCode = result.Error == RegisterUserErrors.EmailAlreadyInUse ||
            result.Error == RegisterUserErrors.UsernameAlreadyInUse
                ? StatusCodes.Status409Conflict
                : StatusCodes.Status400BadRequest;

        string title = statusCode == StatusCodes.Status409Conflict
            ? "A registration conflict occurred."
            : "The registration request is invalid.";

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
