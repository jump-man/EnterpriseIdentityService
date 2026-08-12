using System.Security.Cryptography;
using System.Text;
using EnterpriseIdentityService.Application.Abstractions.Authentication;

namespace EnterpriseIdentityService.Infrastructure.Authentication;

internal sealed class PasswordResetTokenHasher : IPasswordResetTokenHasher
{
    public string Hash(string rawToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawToken);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
    }
}
