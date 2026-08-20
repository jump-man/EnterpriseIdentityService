using EnterpriseIdentityService.Application.Authentication;
using Microsoft.Extensions.Options;

namespace EnterpriseIdentityService.Infrastructure.Authentication;

internal sealed class AuthenticationSessionOptionsValidator
    : IValidateOptions<AuthenticationSessionOptions>
{
    public ValidateOptionsResult Validate(
        string? name,
        AuthenticationSessionOptions options) =>
        options.Lifetime > TimeSpan.Zero
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                "AuthenticationSessions:Lifetime must be positive.");
}
