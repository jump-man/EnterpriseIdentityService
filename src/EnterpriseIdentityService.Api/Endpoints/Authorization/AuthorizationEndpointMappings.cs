using EnterpriseIdentityService.Api.Extensions;
using EnterpriseIdentityService.Application.Authorization;
using EnterpriseIdentityService.Application.Authorization.PermissionCatalog;
using EnterpriseIdentityService.Application.Authorization.Roles;
using EnterpriseIdentityService.Application.Authorization.UserRoles;
using EnterpriseIdentityService.Domain.Roles;
using EnterpriseIdentityService.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseIdentityService.Api.Endpoints.Authorization;

internal static class AuthorizationEndpointMappings
{
    public static IEndpointRouteBuilder MapAuthorizationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/permissions", ListPermissionsAsync)
            .WithName("ListPermissions").WithTags("Authorization")
            .RequirePermission(Permissions.Roles.Read)
            .Produces<IReadOnlyList<PermissionResponse>>();

        endpoints.MapGet("/api/roles", ListRolesAsync)
            .WithName("ListRoles").WithTags("Authorization")
            .RequirePermission(Permissions.Roles.Read)
            .Produces<IReadOnlyList<RoleResponse>>();
        endpoints.MapGet("/api/roles/{roleId:guid}", GetRoleAsync)
            .WithName("GetRole").WithTags("Authorization")
            .RequirePermission(Permissions.Roles.Read)
            .Produces<RoleResponse>().Produces<ProblemDetails>(404);
        endpoints.MapPost("/api/roles", CreateRoleAsync)
            .WithName("CreateRole").WithTags("Authorization")
            .RequirePermission(Permissions.Roles.Manage)
            .RequireRateLimiting("authorization-mutation")
            .Produces<RoleResponse>(201).Produces<ProblemDetails>(400).Produces<ProblemDetails>(409);
        endpoints.MapPut("/api/roles/{roleId:guid}/name", RenameRoleAsync)
            .WithName("RenameRole").WithTags("Authorization")
            .RequirePermission(Permissions.Roles.Manage)
            .RequireRateLimiting("authorization-mutation")
            .Produces<RoleResponse>().Produces<ProblemDetails>(400).Produces<ProblemDetails>(409);
        endpoints.MapPost("/api/roles/{roleId:guid}/enable", EnableRoleAsync)
            .WithName("EnableRole").WithTags("Authorization")
            .RequirePermission(Permissions.Roles.Manage)
            .RequireRateLimiting("authorization-mutation");
        endpoints.MapPost("/api/roles/{roleId:guid}/disable", DisableRoleAsync)
            .WithName("DisableRole").WithTags("Authorization")
            .RequirePermission(Permissions.Roles.Manage)
            .RequireRateLimiting("authorization-mutation");
        endpoints.MapDelete("/api/roles/{roleId:guid}", DeleteRoleAsync)
            .WithName("DeleteRole").WithTags("Authorization")
            .RequirePermission(Permissions.Roles.Manage)
            .RequireRateLimiting("authorization-mutation");
        endpoints.MapGet("/api/roles/{roleId:guid}/permissions", GetRolePermissionsAsync)
            .WithName("GetRolePermissions").WithTags("Authorization")
            .RequirePermission(Permissions.Roles.Read);
        endpoints.MapPut("/api/roles/{roleId:guid}/permissions", ReplaceRolePermissionsAsync)
            .WithName("ReplaceRolePermissions").WithTags("Authorization")
            .RequirePermission(Permissions.Roles.Manage)
            .RequireRateLimiting("authorization-mutation");

        endpoints.MapGet("/api/users/{userId:guid}/roles", ListUserRolesAsync)
            .WithName("ListUserRoles").WithTags("Authorization")
            .RequirePermission(Permissions.UserRoles.Read);
        endpoints.MapPost("/api/users/{userId:guid}/roles/{roleId:guid}", AssignRoleAsync)
            .WithName("AssignRole").WithTags("Authorization")
            .RequirePermission(Permissions.UserRoles.Manage)
            .RequireRateLimiting("authorization-mutation");
        endpoints.MapDelete("/api/users/{userId:guid}/roles/{roleId:guid}", RemoveRoleAsync)
            .WithName("RemoveRole").WithTags("Authorization")
            .RequirePermission(Permissions.UserRoles.Manage)
            .RequireRateLimiting("authorization-mutation");

        return endpoints;
    }

    private static async Task<IResult> ListPermissionsAsync(
        ListPermissionsQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new ListPermissionsQuery(), cancellationToken);
        return Results.Ok(result.Value.Select(identifier => new PermissionResponse(identifier)));
    }

    private static async Task<IResult> ListRolesAsync(
        ListRolesQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new ListRolesQuery(), cancellationToken);
        return Results.Ok(result.Value.Select(ToResponse));
    }

    private static async Task<IResult> GetRoleAsync(
        Guid roleId,
        GetRoleQueryHandler handler,
        CancellationToken cancellationToken)
    {
        if (roleId == Guid.Empty) return InvalidIdentifier();
        var result = await handler.Handle(new GetRoleQuery(new RoleId(roleId)), cancellationToken);
        return result.IsSuccess ? Results.Ok(ToResponse(result.Value)) : result.ToProblem();
    }

    private static async Task<IResult> CreateRoleAsync(
        CreateRoleRequest request,
        CreateRoleCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new CreateRoleCommand(request.Name), cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/roles/{result.Value.Id}", ToResponse(result.Value))
            : result.ToProblem();
    }

    private static async Task<IResult> RenameRoleAsync(
        Guid roleId,
        RenameRoleRequest request,
        RenameRoleCommandHandler handler,
        CancellationToken cancellationToken)
    {
        if (roleId == Guid.Empty) return InvalidIdentifier();
        var result = await handler.Handle(
            new RenameRoleCommand(new RoleId(roleId), request.Name), cancellationToken);
        return result.IsSuccess ? Results.Ok(ToResponse(result.Value)) : result.ToProblem();
    }

    private static Task<IResult> EnableRoleAsync(
        Guid roleId, HttpContext context, SetRoleEnabledCommandHandler handler,
        CancellationToken cancellationToken) =>
        SetRoleEnabledAsync(roleId, true, context, handler, cancellationToken);

    private static Task<IResult> DisableRoleAsync(
        Guid roleId, HttpContext context, SetRoleEnabledCommandHandler handler,
        CancellationToken cancellationToken) =>
        SetRoleEnabledAsync(roleId, false, context, handler, cancellationToken);

    private static async Task<IResult> SetRoleEnabledAsync(
        Guid roleId, bool enabled, HttpContext context, SetRoleEnabledCommandHandler handler,
        CancellationToken cancellationToken)
    {
        if (roleId == Guid.Empty || !context.User.TryGetUserId(out UserId actorId))
            return Results.Unauthorized();
        var result = await handler.Handle(
            new SetRoleEnabledCommand(actorId, new RoleId(roleId), enabled), cancellationToken);
        return result.IsSuccess ? Results.Ok(ToResponse(result.Value)) : result.ToProblem();
    }

    private static async Task<IResult> DeleteRoleAsync(
        Guid roleId, DeleteRoleCommandHandler handler, CancellationToken cancellationToken)
    {
        if (roleId == Guid.Empty) return InvalidIdentifier();
        var result = await handler.Handle(new DeleteRoleCommand(new RoleId(roleId)), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : result.ToProblem();
    }

    private static async Task<IResult> GetRolePermissionsAsync(
        Guid roleId, GetRoleQueryHandler handler, CancellationToken cancellationToken)
    {
        if (roleId == Guid.Empty) return InvalidIdentifier();
        var result = await handler.Handle(new GetRoleQuery(new RoleId(roleId)), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value.Permissions) : result.ToProblem();
    }

    private static async Task<IResult> ReplaceRolePermissionsAsync(
        Guid roleId, ReplaceRolePermissionsRequest request, HttpContext context,
        ReplaceRolePermissionsCommandHandler handler, CancellationToken cancellationToken)
    {
        if (roleId == Guid.Empty || !context.User.TryGetUserId(out UserId actorId))
            return Results.Unauthorized();
        var result = await handler.Handle(new ReplaceRolePermissionsCommand(
            actorId, new RoleId(roleId), request.Permissions), cancellationToken);
        return result.IsSuccess ? Results.Ok(ToResponse(result.Value)) : result.ToProblem();
    }

    private static async Task<IResult> ListUserRolesAsync(
        Guid userId, ListUserRolesQueryHandler handler, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty) return InvalidIdentifier();
        var result = await handler.Handle(new ListUserRolesQuery(new UserId(userId)), cancellationToken);
        return result.IsSuccess
            ? Results.Ok(result.Value.Select(ToResponse))
            : result.ToProblem();
    }

    private static async Task<IResult> AssignRoleAsync(
        Guid userId, Guid roleId, HttpContext context, AssignRoleCommandHandler handler,
        CancellationToken cancellationToken)
    {
        if (!TryGetRoleChangeIds(userId, roleId, context, out UserId actorId))
            return Results.Unauthorized();
        var result = await handler.Handle(new AssignRoleCommand(
            actorId, new UserId(userId), new RoleId(roleId)), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : result.ToProblem();
    }

    private static async Task<IResult> RemoveRoleAsync(
        Guid userId, Guid roleId, HttpContext context, RemoveRoleCommandHandler handler,
        CancellationToken cancellationToken)
    {
        if (!TryGetRoleChangeIds(userId, roleId, context, out UserId actorId))
            return Results.Unauthorized();
        var result = await handler.Handle(new RemoveRoleCommand(
            actorId, new UserId(userId), new RoleId(roleId)), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : result.ToProblem();
    }

    private static bool TryGetRoleChangeIds(
        Guid userId, Guid roleId, HttpContext context, out UserId actorId)
    {
        actorId = default;
        return userId != Guid.Empty && roleId != Guid.Empty &&
            context.User.TryGetUserId(out actorId);
    }

    private static RoleResponse ToResponse(RoleResult role) => new(
        role.Id, role.Name, role.IsSystem, role.IsEnabled, role.Permissions);

    private static IResult InvalidIdentifier() => Results.Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "The request is invalid.",
        detail: "The identifier cannot be empty.");
}
