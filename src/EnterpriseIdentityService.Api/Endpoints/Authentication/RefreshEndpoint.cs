using EnterpriseIdentityService.Api.Extensions;
using EnterpriseIdentityService.Application.Authentication.Refresh;
using Microsoft.AspNetCore.Mvc;
namespace EnterpriseIdentityService.Api.Endpoints.Authentication;
internal static class RefreshEndpoint
{
    public static IEndpointRouteBuilder MapRefreshEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/auth/refresh", HandleAsync).WithName("Refresh").WithTags("Authentication")
            .AllowAnonymous().RequireRateLimiting("token-refresh").Produces<LoginResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json");
        return endpoints;
    }
    private static async Task<IResult> HandleAsync(RefreshRequest request, RefreshCommandHandler handler,
        TimeProvider timeProvider, CancellationToken ct)
    {
        var result = await handler.Handle(new RefreshCommand(request.RefreshToken), ct);
        if (result.IsFailure) return result.ToProblem();
        long expiresIn = Math.Max(0, (long)(result.Value.AccessTokenExpiresAtUtc - timeProvider.GetUtcNow()).TotalSeconds);
        return Results.Ok(new LoginResponse(result.Value.AccessToken, result.Value.RefreshToken, "Bearer", expiresIn));
    }
}
