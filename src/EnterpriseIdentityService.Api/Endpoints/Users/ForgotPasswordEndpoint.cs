using EnterpriseIdentityService.Api.Extensions;
using EnterpriseIdentityService.Application.Users.ForgotPassword;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseIdentityService.Api.Endpoints.Users;

internal static class ForgotPasswordEndpoint
{
    public static IEndpointRouteBuilder MapForgotPasswordEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/users/forgot-password", HandleAsync).WithName("ForgotPassword").WithTags("Users")
            .AllowAnonymous().RequireRateLimiting("password-recovery")
            .Produces(StatusCodes.Status202Accepted)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(ForgotPasswordRequest request,
        ForgotPasswordCommandHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new ForgotPasswordCommand(request.Email), cancellationToken);
        return result.IsSuccess ? Results.Accepted() : result.ToProblem();
    }
}
