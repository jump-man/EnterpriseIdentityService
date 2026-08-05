namespace EnterpriseIdentityService.Application.Authentication.Login;

public sealed record LoginResult(string AccessToken, DateTimeOffset ExpiresAtUtc);
