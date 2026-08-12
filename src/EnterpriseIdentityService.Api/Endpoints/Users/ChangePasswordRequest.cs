namespace EnterpriseIdentityService.Api.Endpoints.Users;

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
