using EnterpriseIdentityService.Application.Abstractions.Messaging;

namespace EnterpriseIdentityService.Application.Users.VerifyEmail;

public sealed record VerifyEmailCommand(string Token) : ICommand;
