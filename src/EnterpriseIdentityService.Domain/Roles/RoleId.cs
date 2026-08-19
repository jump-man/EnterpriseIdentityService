namespace EnterpriseIdentityService.Domain.Roles;

public readonly record struct RoleId
{
    public RoleId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A role identifier cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static RoleId New() => new(Guid.NewGuid());
}
