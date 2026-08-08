namespace EnterpriseIdentityService.Domain.Users;

public readonly record struct EmailVerificationTokenId(Guid Value)
{
    public static EmailVerificationTokenId New() => new(Guid.NewGuid());
}
