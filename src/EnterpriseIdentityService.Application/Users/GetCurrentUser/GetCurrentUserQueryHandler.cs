using EnterpriseIdentityService.Application.Abstractions;
using EnterpriseIdentityService.Application.Abstractions.Messaging;
using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Domain.Users;

namespace EnterpriseIdentityService.Application.Users.GetCurrentUser;

public sealed class GetCurrentUserQueryHandler(IUserRepository userRepository)
    : ICommandHandler<GetCurrentUserQuery, CurrentUserResult>
{
    public async Task<Result<CurrentUserResult>> Handle(
        GetCurrentUserQuery command,
        CancellationToken cancellationToken)
    {
        User? user = await userRepository.GetByIdAsync(command.UserId, cancellationToken);

        return user is null
            ? Result<CurrentUserResult>.Failure(GetCurrentUserErrors.NotFound)
            : Result<CurrentUserResult>.Success(new CurrentUserResult(
                user.Id.Value,
                user.Email.Value,
                user.Status.ToString()));
    }
}
