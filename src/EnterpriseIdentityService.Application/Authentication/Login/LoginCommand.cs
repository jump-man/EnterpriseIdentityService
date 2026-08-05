using EnterpriseIdentityService.Application.Abstractions.Messaging;

namespace EnterpriseIdentityService.Application.Authentication.Login;

public sealed record LoginCommand(string Email, string Password) : ICommand<LoginResult>;
