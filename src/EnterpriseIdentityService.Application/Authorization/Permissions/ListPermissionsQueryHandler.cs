using EnterpriseIdentityService.Application.Abstractions;
using EnterpriseIdentityService.Application.Abstractions.Messaging;

namespace EnterpriseIdentityService.Application.Authorization.PermissionCatalog;

public sealed class ListPermissionsQueryHandler
    : ICommandHandler<ListPermissionsQuery, IReadOnlyList<string>>
{
    public Task<Result<IReadOnlyList<string>>> Handle(
        ListPermissionsQuery command,
        CancellationToken cancellationToken) =>
        Task.FromResult(Result<IReadOnlyList<string>>.Success(
            EnterpriseIdentityService.Application.Authorization.Permissions.All));
}
