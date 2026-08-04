using EnterpriseIdentityService.Domain.Users;

namespace EnterpriseIdentityService.Application.Abstractions.Authentication;

public interface IPasswordHasher
{
    PasswordHash Hash(string password);
}
