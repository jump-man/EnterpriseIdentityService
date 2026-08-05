namespace EnterpriseIdentityService.Application.Abstractions.Authentication;

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAtUtc);
