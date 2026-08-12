namespace EnterpriseIdentityService.Application.PasswordRecovery;

public sealed class PasswordRecoveryOptions
{
    public const string SectionName = "PasswordRecovery";
    public TimeSpan TokenLifetime { get; init; } = TimeSpan.FromMinutes(15);
    public TimeSpan RequestCooldown { get; init; } = TimeSpan.FromMinutes(1);
    public string PublicBaseUrl { get; init; } = string.Empty;
}
