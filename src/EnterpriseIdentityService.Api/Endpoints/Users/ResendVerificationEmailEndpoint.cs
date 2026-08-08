using EnterpriseIdentityService.Api.Extensions;
using EnterpriseIdentityService.Application.Users.ResendVerificationEmail;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseIdentityService.Api.Endpoints.Users;

internal static class ResendVerificationEmailEndpoint
{
    public static IEndpointRouteBuilder MapResendVerificationEmailEndpoint(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/users/resend-verification-email", HandleAsync)
            .WithName("ResendVerificationEmail")
            .WithTags("Users")
            .AllowAnonymous()
            .RequireRateLimiting("verification-resend")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        ResendVerificationEmailRequest request,
        ResendVerificationEmailCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new ResendVerificationEmailCommand(request.Email), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : result.ToProblem();
    }
}
