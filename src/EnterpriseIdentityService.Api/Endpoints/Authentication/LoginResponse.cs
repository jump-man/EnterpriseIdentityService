namespace EnterpriseIdentityService.Api.Endpoints.Authentication;

public sealed record LoginResponse(
    string AccessToken,
    string TokenType,
    long ExpiresIn);
