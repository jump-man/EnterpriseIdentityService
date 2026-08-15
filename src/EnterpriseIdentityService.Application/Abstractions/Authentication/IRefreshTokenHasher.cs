namespace EnterpriseIdentityService.Application.Abstractions.Authentication;
public interface IRefreshTokenHasher { string Hash(string rawToken); }
