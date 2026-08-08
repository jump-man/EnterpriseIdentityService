using System.Security.Cryptography;
using System.Text;
using EnterpriseIdentityService.Application.Abstractions.Authentication;

namespace EnterpriseIdentityService.Infrastructure.Authentication;

internal sealed class EmailVerificationTokenHasher : IEmailVerificationTokenHasher
{
    public string Hash(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
