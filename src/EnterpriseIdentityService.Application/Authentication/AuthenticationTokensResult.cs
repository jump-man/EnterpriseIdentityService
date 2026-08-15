namespace EnterpriseIdentityService.Application.Authentication;
public sealed record AuthenticationTokensResult(
    string AccessToken, string RefreshToken, DateTimeOffset AccessTokenExpiresAtUtc);
