using System.Text;

namespace EnterpriseIdentityService.Domain.Users;

public sealed record Username
{
    private Username(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Username Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        string normalizedValue = value.Trim();

        if (normalizedValue.Length is < 3 or > 50 ||
            normalizedValue.EnumerateRunes().Any(rune =>
                !Rune.IsLetterOrDigit(rune) && rune.Value is not ('.' or '_' or '-')))
        {
            throw new ArgumentException("The username is invalid.", nameof(value));
        }

        return new Username(normalizedValue);
    }
}
