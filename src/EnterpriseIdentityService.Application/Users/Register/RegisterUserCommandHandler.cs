using EnterpriseIdentityService.Application.Abstractions;
using EnterpriseIdentityService.Application.Abstractions.Authentication;
using EnterpriseIdentityService.Application.Abstractions.Mailing;
using EnterpriseIdentityService.Application.Abstractions.Messaging;
using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Domain.Users;

namespace EnterpriseIdentityService.Application.Users.Register;

public sealed class RegisterUserCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IEmailVerificationTokenRepository tokenRepository,
    IEmailVerificationTokenGenerator tokenGenerator,
    IEmailVerificationTokenHasher tokenHasher,
    IVerificationEmailFactory emailFactory,
    IEmailSender emailSender,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    Microsoft.Extensions.Options.IOptions<EmailVerification.EmailVerificationOptions> options)
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
        DateTimeOffset nowUtc = timeProvider.GetUtcNow();
        User user = User.Register(
            userId,
            email,
            username,
            passwordHash,
            nowUtc);

        string rawToken = tokenGenerator.Generate();
        var verificationToken = EmailVerificationToken.Create(
            EmailVerificationTokenId.New(),
            userId,
            tokenHasher.Hash(rawToken),
            nowUtc,
            nowUtc.Add(options.Value.TokenLifetime));

        // Infrastructure must also enforce unique email and username constraints.
        userRepository.Add(user);
        tokenRepository.Add(verificationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        EmailMessage message = emailFactory.Create(
            email,
            rawToken,
            verificationToken.ExpiresAtUtc,
            verificationToken.Id.Value.ToString("N"));

        try
        {
            await emailSender.SendAsync(message, cancellationToken);
        }
        catch (EmailDeliveryException)
        {
            return Result<UserId>.Failure(RegisterUserErrors.EmailDeliveryUnavailable);
        }

        return Result<UserId>.Success(userId);
    }

}
