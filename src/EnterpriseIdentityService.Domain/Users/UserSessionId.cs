namespace EnterpriseIdentityService.Domain.Users;

public readonly record struct UserSessionId(Guid Value)
{
    public static UserSessionId New() => new(Guid.NewGuid());
}
