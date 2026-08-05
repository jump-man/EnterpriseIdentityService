namespace EnterpriseIdentityService.Api.Endpoints.Authentication;

public sealed record RegisterUserRequest(
    string Email,
    string Username,
    string Password);
