using Microsoft.Extensions.Options;

namespace EnterpriseIdentityService.Infrastructure.Authentication;

internal sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    private const int MaximumExpirationMinutes = 1440;

    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        List<string> failures = [];

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            failures.Add("Jwt:Issuer is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add("Jwt:Audience is required.");
        }

        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            failures.Add("Jwt:SigningKey is required.");
        }
        else if (options.SigningKey.Length < JwtOptions.MinimumSigningKeyLength)
        {
            failures.Add($"Jwt:SigningKey must be at least {JwtOptions.MinimumSigningKeyLength} characters.");
        }

        if (options.ExpirationMinutes <= 0 || options.ExpirationMinutes > MaximumExpirationMinutes)
        {
            failures.Add($"Jwt:ExpirationMinutes must be between 1 and {MaximumExpirationMinutes}.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
