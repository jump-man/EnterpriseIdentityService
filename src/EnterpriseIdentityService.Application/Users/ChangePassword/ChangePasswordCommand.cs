using EnterpriseIdentityService.Application.Abstractions.Messaging;
using EnterpriseIdentityService.Domain.Users;

namespace EnterpriseIdentityService.Application.Users.ChangePassword;

public sealed record ChangePasswordCommand(
    UserId UserId,
    string CurrentPassword,
    string NewPassword) : ICommand;
