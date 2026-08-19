using EnterpriseIdentityService.Application.Authorization;
using EnterpriseIdentityService.Domain.Users;

namespace EnterpriseIdentityService.Application.Abstractions.Authorization;

public interface IAuthorizationSnapshotProvider
{
    Task<AuthorizationSnapshot> GetAsync(
        User user,
        CancellationToken cancellationToken);
}
