using System.IdentityModel.Tokens.Jwt;
using EnterpriseIdentityService.Api.Extensions;
using EnterpriseIdentityService.Application.Users.ChangePassword;
using EnterpriseIdentityService.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseIdentityService.Api.Endpoints.Users;

internal static class ChangePasswordEndpoint
{
    public static IEndpointRouteBuilder MapChangePasswordEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/users/change-password", HandleAsync)
            .WithName("ChangePassword").WithTags("Users")
            .RequireAuthorization().RequireRateLimiting("password-change")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        ChangePasswordRequest request, HttpContext httpContext,
        ChangePasswordCommandHandler handler, CancellationToken cancellationToken)
    {
        string? subject = httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!Guid.TryParse(subject, out Guid value) || value == Guid.Empty) return Results.Unauthorized();
        var result = await handler.Handle(
            new ChangePasswordCommand(new UserId(value), request.CurrentPassword, request.NewPassword),
            cancellationToken);
        return result.IsSuccess ? Results.NoContent() : result.ToProblem();
    }
}
