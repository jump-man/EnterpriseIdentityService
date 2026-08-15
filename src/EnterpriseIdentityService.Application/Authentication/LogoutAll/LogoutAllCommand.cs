using EnterpriseIdentityService.Application.Abstractions.Messaging;
using EnterpriseIdentityService.Domain.Users;
namespace EnterpriseIdentityService.Application.Authentication.LogoutAll;
public sealed record LogoutAllCommand(UserId UserId) : ICommand;
