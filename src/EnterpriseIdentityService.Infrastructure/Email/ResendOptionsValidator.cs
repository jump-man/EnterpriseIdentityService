using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace EnterpriseIdentityService.Infrastructure.Mailing;

internal sealed class ResendOptionsValidator : IValidateOptions<ResendOptions>
{
    public ValidateOptionsResult Validate(string? name, ResendOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey) ||
            string.IsNullOrWhiteSpace(options.FromName) ||
            !new EmailAddressAttribute().IsValid(options.FromAddress))
        {
            return ValidateOptionsResult.Fail(
                "Enabled Resend delivery requires an API key, a valid FromAddress, and FromName.");
        }

        return ValidateOptionsResult.Success;
    }
}
