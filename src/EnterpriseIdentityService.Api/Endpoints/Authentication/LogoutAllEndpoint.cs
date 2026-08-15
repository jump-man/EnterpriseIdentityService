using System.IdentityModel.Tokens.Jwt;
using EnterpriseIdentityService.Api.Extensions;
using EnterpriseIdentityService.Application.Authentication.LogoutAll;
using EnterpriseIdentityService.Domain.Users;
namespace EnterpriseIdentityService.Api.Endpoints.Authentication;
internal static class LogoutAllEndpoint
{
    public static IEndpointRouteBuilder MapLogoutAllEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/auth/logout-all", HandleAsync).WithName("LogoutAll").WithTags("Authentication")
            .RequireAuthorization().RequireRateLimiting("session-security").Produces(StatusCodes.Status204NoContent);
        return endpoints;
    }
    private static async Task<IResult> HandleAsync(HttpContext context, LogoutAllCommandHandler handler, CancellationToken ct)
    {
        if (!Guid.TryParse(context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out Guid id)) return Results.Unauthorized();
        var result = await handler.Handle(new LogoutAllCommand(new UserId(id)), ct);
        return result.IsSuccess ? Results.NoContent() : result.ToProblem();
    }
}
