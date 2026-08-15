namespace EnterpriseIdentityService.Application.Authentication.Login;

public sealed record LoginResult(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAtUtc);
