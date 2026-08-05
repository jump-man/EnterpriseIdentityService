namespace EnterpriseIdentityService.Api.Endpoints.Users;

public sealed record CurrentUserResponse(Guid Id, string Email, string Status);
