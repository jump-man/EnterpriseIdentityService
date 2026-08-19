namespace EnterpriseIdentityService.Api.Endpoints.Authorization;

internal sealed record PermissionResponse(string Identifier);

internal sealed record RoleResponse(
    Guid Id,
    string Name,
    bool IsSystem,
    bool IsEnabled,
    IReadOnlyCollection<string> Permissions);

internal sealed record CreateRoleRequest(string Name);

internal sealed record RenameRoleRequest(string Name);

internal sealed record ReplaceRolePermissionsRequest(IReadOnlyCollection<string> Permissions);
