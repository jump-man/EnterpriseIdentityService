using EnterpriseIdentityService.Api.Extensions;
using EnterpriseIdentityService.Application.Authentication.Login;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseIdentityService.Api.Endpoints.Authentication;

internal static class LoginEndpoint
{
    public static IEndpointRouteBuilder MapLoginEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/auth/login", HandleAsync)
            .WithName("Login")
            .WithTags("Authentication")
            .AllowAnonymous()
            .Produces<LoginResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json");

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        LoginRequest request,
        LoginCommandHandler handler,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new LoginCommand(request.Email, request.Password),
            cancellationToken);

        if (result.IsFailure)
        {
            return result.ToProblem();
        }

        long expiresIn = Math.Max(
            0,
            (long)(result.Value.ExpiresAtUtc - timeProvider.GetUtcNow()).TotalSeconds);

        return Results.Ok(new LoginResponse(result.Value.AccessToken, result.Value.RefreshToken, "Bearer", expiresIn));
    }
}
