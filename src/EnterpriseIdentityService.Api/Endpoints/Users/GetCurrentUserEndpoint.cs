using System.IdentityModel.Tokens.Jwt;
using EnterpriseIdentityService.Api.Extensions;
using EnterpriseIdentityService.Application.Users.GetCurrentUser;
using EnterpriseIdentityService.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseIdentityService.Api.Endpoints.Users;

internal static class GetCurrentUserEndpoint
{
    public static IEndpointRouteBuilder MapGetCurrentUserEndpoint(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/users/me", HandleAsync)
            .WithName("GetCurrentUser")
            .WithTags("Users")
            .RequireAuthorization()
            .Produces<CurrentUserResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json");

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext httpContext,
        GetCurrentUserQueryHandler handler,
        CancellationToken cancellationToken)
    {
        string? subject = httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!Guid.TryParse(subject, out Guid value) || value == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        var result = await handler.Handle(
            new GetCurrentUserQuery(new UserId(value)),
            cancellationToken);

        return result.IsSuccess
            ? Results.Ok(new CurrentUserResponse(
                result.Value.Id,
                result.Value.Email,
                result.Value.Status))
            : result.ToProblem();
    }
}
