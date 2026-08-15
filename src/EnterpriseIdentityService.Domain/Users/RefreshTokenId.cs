namespace EnterpriseIdentityService.Domain.Users;

public readonly record struct RefreshTokenId(Guid Value)
{
    public static RefreshTokenId New() => new(Guid.NewGuid());
}
