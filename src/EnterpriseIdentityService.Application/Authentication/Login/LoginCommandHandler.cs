using EnterpriseIdentityService.Application.Abstractions;
using EnterpriseIdentityService.Application.Abstractions.Authentication;
using EnterpriseIdentityService.Application.Abstractions.Messaging;
using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Domain.Users;

namespace EnterpriseIdentityService.Application.Authentication.Login;

public sealed class LoginCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IAccessTokenProvider accessTokenProvider)
    : ICommandHandler<LoginCommand, LoginResult>
{
    public async Task<Result<LoginResult>> Handle(
        LoginCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.Email))
        {
            return Result<LoginResult>.Failure(LoginErrors.EmailRequired);
        }

        if (string.IsNullOrWhiteSpace(command.Password))
        {
            return Result<LoginResult>.Failure(LoginErrors.PasswordRequired);
        }

        Email email;
        try
        {
            email = Email.Create(command.Email);
        }
        catch (ArgumentException)
        {
            return Result<LoginResult>.Failure(LoginErrors.InvalidEmail);
        }

        User? user = await userRepository.GetByEmailAsync(email, cancellationToken);

        if (user is null ||
            !passwordHasher.Verify(command.Password, user.PasswordHash) ||
            user.Status != UserStatus.Active)
        {
            return Result<LoginResult>.Failure(LoginErrors.InvalidCredentials);
        }

        AccessToken token = accessTokenProvider.Generate(user);

        return Result<LoginResult>.Success(new LoginResult(token.Value, token.ExpiresAtUtc));
    }
}
