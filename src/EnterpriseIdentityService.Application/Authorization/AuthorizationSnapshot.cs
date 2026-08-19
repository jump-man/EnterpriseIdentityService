namespace EnterpriseIdentityService.Application.Authorization;

public sealed record AuthorizationSnapshot(
    int AuthorizationVersion,
    IReadOnlyCollection<string> Permissions);
