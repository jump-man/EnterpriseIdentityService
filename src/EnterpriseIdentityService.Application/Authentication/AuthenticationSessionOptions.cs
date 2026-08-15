namespace EnterpriseIdentityService.Application.Authentication;
public sealed class AuthenticationSessionOptions
{
    public const string SectionName = "AuthenticationSessions";
    public TimeSpan Lifetime { get; init; } = TimeSpan.FromDays(30);
}
