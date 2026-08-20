using EnterpriseIdentityService.Application.PasswordRecovery;
using Microsoft.Extensions.Options;

namespace EnterpriseIdentityService.Infrastructure.Mailing;

internal sealed class PasswordRecoveryOptionsValidator
    : IValidateOptions<PasswordRecoveryOptions>
{
    public ValidateOptionsResult Validate(
        string? name,
        PasswordRecoveryOptions options)
    {
        List<string> failures = [];

        if (options.TokenLifetime <= TimeSpan.Zero)
        {
            failures.Add("PasswordRecovery:TokenLifetime must be positive.");
        }

        if (options.RequestCooldown < TimeSpan.Zero)
        {
            failures.Add("PasswordRecovery:RequestCooldown cannot be negative.");
        }

        if (!Uri.TryCreate(options.PublicBaseUrl, UriKind.Absolute, out Uri? uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            failures.Add("PasswordRecovery:PublicBaseUrl must be an absolute HTTPS URL.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
