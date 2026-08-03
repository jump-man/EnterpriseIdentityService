namespace EnterpriseIdentityService.Domain.Users;

public readonly record struct UserId
{
    public UserId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A user identifier cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static UserId New() => new(Guid.NewGuid());
}
