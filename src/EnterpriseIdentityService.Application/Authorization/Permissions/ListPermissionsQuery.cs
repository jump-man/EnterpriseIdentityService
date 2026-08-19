using EnterpriseIdentityService.Application.Abstractions.Messaging;

namespace EnterpriseIdentityService.Application.Authorization.PermissionCatalog;

public sealed record ListPermissionsQuery : ICommand<IReadOnlyList<string>>;
