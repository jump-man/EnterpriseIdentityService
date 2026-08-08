using EnterpriseIdentityService.Api.Extensions;
using EnterpriseIdentityService.Application.Users.VerifyEmail;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseIdentityService.Api.Endpoints.Users;

internal static class VerifyEmailEndpoint
{
    public static IEndpointRouteBuilder MapVerifyEmailEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/users/verify-email", HandleAsync)
            .WithName("VerifyEmail")
            .WithTags("Users")
            .AllowAnonymous()
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        VerifyEmailRequest request,
        VerifyEmailCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new VerifyEmailCommand(request.Token), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : result.ToProblem();
    }
}
