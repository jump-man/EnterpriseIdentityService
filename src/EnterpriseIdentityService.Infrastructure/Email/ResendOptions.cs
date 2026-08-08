namespace EnterpriseIdentityService.Infrastructure.Mailing;

public sealed class ResendOptions
{
    public const string SectionName = "Resend";

    public bool Enabled { get; init; }
    public string ApiKey { get; init; } = string.Empty;
    public string FromAddress { get; init; } = string.Empty;
    public string FromName { get; init; } = string.Empty;
}
