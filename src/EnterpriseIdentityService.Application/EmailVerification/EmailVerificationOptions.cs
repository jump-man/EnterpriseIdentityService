namespace EnterpriseIdentityService.Application.EmailVerification;

public sealed class EmailVerificationOptions
{
    public const string SectionName = "EmailVerification";

    public TimeSpan TokenLifetime { get; init; } = TimeSpan.FromHours(24);
    public TimeSpan ResendCooldown { get; init; } = TimeSpan.FromMinutes(1);
    public string PublicBaseUrl { get; init; } = string.Empty;
}
