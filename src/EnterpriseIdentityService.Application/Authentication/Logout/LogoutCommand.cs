using EnterpriseIdentityService.Application.Abstractions.Messaging;
using EnterpriseIdentityService.Domain.Users;
namespace EnterpriseIdentityService.Application.Authentication.Logout;
public sealed record LogoutCommand(UserId UserId, UserSessionId SessionId) : ICommand;
