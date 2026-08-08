namespace EnterpriseIdentityService.Application.Abstractions.Authentication;

public interface IEmailVerificationTokenGenerator
{
    string Generate();
}
