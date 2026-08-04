using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseIdentityService.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(ApplicationDbContext dbContext) : IUserRepository
{
    public Task<bool> ExistsByEmailAsync(
        Email email,
        CancellationToken cancellationToken)
    {
        return dbContext.Users.AnyAsync(
            user => user.Email == email,
            cancellationToken);
    }

    public Task<bool> ExistsByUsernameAsync(
        Username username,
        CancellationToken cancellationToken)
    {
        return dbContext.Users.AnyAsync(
            user => user.Username == username,
            cancellationToken);
    }

    public void Add(User user)
    {
        dbContext.Users.Add(user);
    }
}
