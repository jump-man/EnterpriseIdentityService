using EnterpriseIdentityService.Application.Abstractions.Authentication;
using EnterpriseIdentityService.Domain.Users;
using Microsoft.AspNetCore.Identity;

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

    public bool Verify(string password, PasswordHash passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentNullException.ThrowIfNull(passwordHash);

        PasswordVerificationResult result = _hasher.VerifyHashedPassword(
            new PasswordHasherUser(), passwordHash.Value, password);

        return result != PasswordVerificationResult.Failed;
    }

    private sealed class PasswordHasherUser;
}
