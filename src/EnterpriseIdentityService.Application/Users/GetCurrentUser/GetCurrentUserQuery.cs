using EnterpriseIdentityService.Application.Abstractions.Messaging;
using EnterpriseIdentityService.Domain.Users;

namespace EnterpriseIdentityService.Application.Users.GetCurrentUser;

public sealed record GetCurrentUserQuery(UserId UserId) : ICommand<CurrentUserResult>;
