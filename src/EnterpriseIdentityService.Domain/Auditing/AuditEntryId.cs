namespace EnterpriseIdentityService.Domain.Auditing;

public readonly record struct AuditEntryId
{
    public AuditEntryId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("An audit entry identifier cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static AuditEntryId New() => new(Guid.NewGuid());
}
