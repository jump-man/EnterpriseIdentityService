using EnterpriseIdentityService.Domain.Users;

namespace EnterpriseIdentityService.Application.Abstractions.Persistence;

public interface IUserRepository
{
    Task<bool> ExistsByEmailAsync(Email email, CancellationToken cancellationToken);

    Task<bool> ExistsByUsernameAsync(Username username, CancellationToken cancellationToken);

    void Add(User user);
}
