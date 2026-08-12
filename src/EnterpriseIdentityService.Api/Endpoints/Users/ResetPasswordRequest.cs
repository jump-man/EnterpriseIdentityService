namespace EnterpriseIdentityService.Api.Endpoints.Users;

public sealed record ResetPasswordRequest(string Token, string NewPassword);
