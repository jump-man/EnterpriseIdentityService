namespace EnterpriseIdentityService.Application.Abstractions.Authentication;

public interface IPasswordResetTokenHasher { string Hash(string rawToken); }
