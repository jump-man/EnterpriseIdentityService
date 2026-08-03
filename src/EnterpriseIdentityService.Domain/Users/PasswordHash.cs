namespace EnterpriseIdentityService.Domain.Users;

public sealed record PasswordHash
{
    private PasswordHash(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static PasswordHash Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        return new PasswordHash(value);
    }
}
