using EnterpriseIdentityService.Application.Abstractions.Messaging;

namespace EnterpriseIdentityService.Application.Users.ResendVerificationEmail;

public sealed record ResendVerificationEmailCommand(string Email) : ICommand;
