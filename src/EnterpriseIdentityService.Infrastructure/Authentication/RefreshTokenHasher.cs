using System.Security.Cryptography;
using System.Text;
using EnterpriseIdentityService.Application.Abstractions.Authentication;
namespace EnterpriseIdentityService.Infrastructure.Authentication;
internal sealed class RefreshTokenHasher : IRefreshTokenHasher
{
    public string Hash(string rawToken) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
