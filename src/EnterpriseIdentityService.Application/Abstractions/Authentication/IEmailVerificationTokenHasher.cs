namespace EnterpriseIdentityService.Application.Abstractions.Authentication;

public interface IEmailVerificationTokenHasher
{
    string Hash(string token);
}
