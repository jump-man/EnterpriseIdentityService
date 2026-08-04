using EnterpriseIdentityService.Application.Abstractions.Authentication;
using EnterpriseIdentityService.Domain.Users;

namespace EnterpriseIdentityService.Infrastructure.Authentication;

internal sealed class PasswordHasher : IPasswordHasher
{
    private readonly Microsoft.AspNetCore.Identity.PasswordHasher<PasswordHasherUser> _hasher = new();

    public PasswordHash Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        string encodedHash = _hasher.HashPassword(new PasswordHasherUser(), password);

        return PasswordHash.Create(encodedHash);
    }

    private sealed class PasswordHasherUser;
}
