namespace EnterpriseIdentityService.Application.Users.GetCurrentUser;

public sealed record CurrentUserResult(Guid Id, string Email, string Status);
