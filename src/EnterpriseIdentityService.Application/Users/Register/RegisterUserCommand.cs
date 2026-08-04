using EnterpriseIdentityService.Application.Abstractions.Messaging;
using EnterpriseIdentityService.Domain.Users;

namespace EnterpriseIdentityService.Application.Users.Register;

public sealed record RegisterUserCommand(
    string Email,
    string Username,
    string Password) : ICommand<UserId>;
