using EnterpriseIdentityService.Api.Extensions;
using EnterpriseIdentityService.Application.Users.ResetPassword;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseIdentityService.Api.Endpoints.Users;

internal static class ResetPasswordEndpoint
{
    public static IEndpointRouteBuilder MapResetPasswordEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/users/reset-password", HandleAsync).WithName("ResetPassword").WithTags("Users")
            .AllowAnonymous().RequireRateLimiting("password-recovery")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(ResetPasswordRequest request,
        ResetPasswordCommandHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new ResetPasswordCommand(request.Token, request.NewPassword), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : result.ToProblem();
    }
}
