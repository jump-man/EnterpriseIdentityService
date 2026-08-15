namespace EnterpriseIdentityService.Api.Endpoints.Authentication;

public sealed record LoginResponse(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    long ExpiresIn);
