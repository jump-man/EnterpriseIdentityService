using EnterpriseIdentityService.Application.Abstractions.Messaging;

namespace EnterpriseIdentityService.Application.Users.ResetPassword;

public sealed record ResetPasswordCommand(string Token, string NewPassword) : ICommand;
