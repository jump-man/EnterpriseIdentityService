using System.Security.Cryptography;
using EnterpriseIdentityService.Application.Abstractions.Authentication;
using Microsoft.AspNetCore.WebUtilities;

namespace EnterpriseIdentityService.Infrastructure.Authentication;

internal sealed class EmailVerificationTokenGenerator : IEmailVerificationTokenGenerator
{
    public string Generate() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
}
