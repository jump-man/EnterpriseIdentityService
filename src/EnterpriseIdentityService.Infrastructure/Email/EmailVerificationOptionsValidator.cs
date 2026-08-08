using EnterpriseIdentityService.Application.EmailVerification;
using Microsoft.Extensions.Options;

namespace EnterpriseIdentityService.Infrastructure.Mailing;

internal sealed class EmailVerificationOptionsValidator : IValidateOptions<EmailVerificationOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailVerificationOptions options)
    {
        if (options.TokenLifetime <= TimeSpan.Zero || options.ResendCooldown <= TimeSpan.Zero)
        {
            return ValidateOptionsResult.Fail("TokenLifetime and ResendCooldown must be positive.");
        }

        if (!Uri.TryCreate(options.PublicBaseUrl, UriKind.Absolute, out Uri? uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            return ValidateOptionsResult.Fail("PublicBaseUrl must be an absolute HTTPS URL.");
        }

        return ValidateOptionsResult.Success;
    }
}
