using System.Security.Cryptography;
using EnterpriseIdentityService.Application.Abstractions.Authentication;
using Microsoft.AspNetCore.WebUtilities;

namespace EnterpriseIdentityService.Infrastructure.Authentication;

internal sealed class PasswordResetTokenGenerator : IPasswordResetTokenGenerator
{
    public string Generate() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
}
