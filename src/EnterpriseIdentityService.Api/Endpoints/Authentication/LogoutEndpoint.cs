using System.IdentityModel.Tokens.Jwt;
using EnterpriseIdentityService.Application.Authentication.Logout;
using EnterpriseIdentityService.Domain.Users;
namespace EnterpriseIdentityService.Api.Endpoints.Authentication;
internal static class LogoutEndpoint
{
    public static IEndpointRouteBuilder MapLogoutEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/auth/logout", HandleAsync).WithName("Logout").WithTags("Authentication")
            .RequireAuthorization().Produces(StatusCodes.Status204NoContent);
        return endpoints;
    }
    private static async Task<IResult> HandleAsync(HttpContext context, LogoutCommandHandler handler, CancellationToken ct)
    {
        if (!Guid.TryParse(context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out Guid userId) ||
            !Guid.TryParse(context.User.FindFirst("sid")?.Value, out Guid sessionId)) return Results.Unauthorized();
        await handler.Handle(new LogoutCommand(new UserId(userId), new UserSessionId(sessionId)), ct);
        return Results.NoContent();
    }
}
