namespace EnterpriseIdentityService.Application.Authorization;

public sealed record RoleResult(
    Guid Id,
    string Name,
    bool IsSystem,
    bool IsEnabled,
    IReadOnlyCollection<string> Permissions);
