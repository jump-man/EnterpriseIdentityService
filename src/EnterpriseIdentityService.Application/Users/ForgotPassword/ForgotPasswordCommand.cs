using EnterpriseIdentityService.Application.Abstractions.Messaging;

namespace EnterpriseIdentityService.Application.Users.ForgotPassword;

public sealed record ForgotPasswordCommand(string Email) : ICommand;
