using EnterpriseIdentityService.Application.Abstractions;
using EnterpriseIdentityService.Application.Abstractions.Authentication;
using EnterpriseIdentityService.Application.Abstractions.Messaging;
using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Domain.Users;

namespace EnterpriseIdentityService.Application.Users.Register;

public sealed class RegisterUserCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RegisterUserCommand, UserId>
{
    public async Task<Result<UserId>> Handle(
        RegisterUserCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.Email))
        {
            return Result<UserId>.Failure(RegisterUserErrors.EmailRequired);
        }

        if (string.IsNullOrWhiteSpace(command.Username))
        {
            return Result<UserId>.Failure(RegisterUserErrors.UsernameRequired);
        }

        if (string.IsNullOrWhiteSpace(command.Password))
        {
            return Result<UserId>.Failure(RegisterUserErrors.PasswordRequired);
        }

        Email email;
        try
        {
            email = Email.Create(command.Email);
        }
        catch (ArgumentException)
        {
            return Result<UserId>.Failure(RegisterUserErrors.InvalidEmail);
        }

        Username username;
        try
        {
            username = Username.Create(command.Username);
        }
        catch (ArgumentException)
        {
            return Result<UserId>.Failure(RegisterUserErrors.InvalidUsername);
        }

        if (await userRepository.ExistsByEmailAsync(email, cancellationToken))
        {
            return Result<UserId>.Failure(RegisterUserErrors.EmailAlreadyInUse);
        }

        if (await userRepository.ExistsByUsernameAsync(username, cancellationToken))
        {
            return Result<UserId>.Failure(RegisterUserErrors.UsernameAlreadyInUse);
        }

        PasswordHash passwordHash = passwordHasher.Hash(command.Password);
        UserId userId = UserId.New();
        User user = User.Register(
            userId,
            email,
            username,
            passwordHash,
            DateTimeOffset.UtcNow);

        // Infrastructure must also enforce unique email and username constraints.
        userRepository.Add(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<UserId>.Success(userId);
    }
}
