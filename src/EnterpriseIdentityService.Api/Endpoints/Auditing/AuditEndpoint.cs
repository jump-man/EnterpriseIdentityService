using EnterpriseIdentityService.Api.Extensions;
using EnterpriseIdentityService.Application.Auditing;
using EnterpriseIdentityService.Application.Authorization;
using EnterpriseIdentityService.Domain.Roles;
using EnterpriseIdentityService.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseIdentityService.Api.Endpoints.Auditing;

internal static class AuditEndpoint
{
    public static IEndpointRouteBuilder MapAuditEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/audit", HandleAsync)
            .WithName("QuerySecurityAudit")
            .WithTags("Audit")
            .WithDescription(
                "Returns a bounded newest-first security audit page. userId matches actor or target identity.")
            .RequirePermission(Permissions.Audit.Read)
            .RequireRateLimiting("audit-read")
            .Produces<AuditPageResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status429TooManyRequests);

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        [FromQuery] Guid? userId,
        [FromQuery] Guid? roleId,
        [FromQuery] Guid? sessionId,
        [FromQuery] string? eventType,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? correlationId,
        [FromQuery] string? cursor,
        [FromQuery] int? pageSize,
        QueryAuditEntriesQueryHandler handler,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty || roleId == Guid.Empty || sessionId == Guid.Empty)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "The request is invalid.",
                detail: "Filter identifiers cannot be empty.");
        }

        var result = await handler.Handle(
            new QueryAuditEntriesQuery(
                userId.HasValue ? new UserId(userId.Value) : null,
                roleId.HasValue ? new RoleId(roleId.Value) : null,
                sessionId.HasValue ? new UserSessionId(sessionId.Value) : null,
                eventType,
                from,
                to,
                correlationId,
                cursor,
                pageSize ?? 50),
            cancellationToken);

        return result.IsSuccess
            ? Results.Ok(new AuditPageResponse(
                result.Value.Items.Select(item => new AuditEntryResponse(
                    item.Id,
                    item.EventType,
                    item.Outcome,
                    item.ReasonCode,
                    item.OccurredAtUtc,
                    item.ActorUserId,
                    item.TargetUserId,
                    item.RoleId,
                    item.SessionId,
                    item.CorrelationId,
                    item.IpAddress,
                    item.UserAgent,
                    item.Permission)).ToArray(),
                result.Value.NextCursor))
            : result.ToProblem();
    }
}
