namespace EnterpriseIdentityService.Domain.Users;

public readonly record struct PasswordResetTokenId(Guid Value)
{
    public static PasswordResetTokenId New() => new(Guid.NewGuid());
}
