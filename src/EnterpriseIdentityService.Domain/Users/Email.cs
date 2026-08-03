using System.Globalization;

namespace EnterpriseIdentityService.Domain.Users;

public sealed record Email
{
    private Email(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Email Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        string normalizedValue = value.Trim().ToLower(CultureInfo.InvariantCulture);
        int separatorIndex = normalizedValue.IndexOf('@');

        if (separatorIndex <= 0 ||
            separatorIndex != normalizedValue.LastIndexOf('@') ||
            separatorIndex == normalizedValue.Length - 1)
        {
            throw new ArgumentException("The email address is invalid.", nameof(value));
        }

        string localPart = normalizedValue[..separatorIndex];
        string domainPart = normalizedValue[(separatorIndex + 1)..];

        if (normalizedValue.Length > 254 ||
            localPart.Length > 64 ||
            localPart.StartsWith('.') ||
            localPart.EndsWith('.') ||
            localPart.Contains("..", StringComparison.Ordinal) ||
            domainPart.StartsWith('.') ||
            domainPart.EndsWith('.') ||
            domainPart.StartsWith('-') ||
            domainPart.EndsWith('-') ||
            domainPart.Contains("..", StringComparison.Ordinal) ||
            normalizedValue.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("The email address is invalid.", nameof(value));
        }

        return new Email(normalizedValue);
    }
}
