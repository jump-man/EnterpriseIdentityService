using EnterpriseIdentityService.Application.Abstractions;
using EnterpriseIdentityService.Application.Abstractions.Authentication;
using EnterpriseIdentityService.Application.Abstractions.Messaging;
using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Domain.Users;
using EnterpriseIdentityService.Application.Authentication;
using Microsoft.Extensions.Options;
using EnterpriseIdentityService.Application.Abstractions.Authorization;
using EnterpriseIdentityService.Application.Authorization;
using EnterpriseIdentityService.Application.Auditing;
using EnterpriseIdentityService.Domain.Auditing;

namespace EnterpriseIdentityService.Application.Authentication.Login;

public sealed class LoginCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IAccessTokenProvider accessTokenProvider,
    IUserSessionRepository sessions,
    IRefreshTokenGenerator refreshTokenGenerator,
    IRefreshTokenHasher refreshTokenHasher,
    IAuthorizationSnapshotProvider authorizationSnapshotProvider,
    AuditRecorder audit,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IOptions<AuthenticationSessionOptions> sessionOptions)
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
            audit.Record(
                AuditEventType.LoginFailed,
                AuditOutcome.Failure,
                AuditReasonCode.InvalidCredentials);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<LoginResult>.Failure(LoginErrors.InvalidCredentials);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        var session = UserSession.Create(UserSessionId.New(), user.Id, user.TokenVersion,
            now, now.Add(sessionOptions.Value.Lifetime));
        string rawRefreshToken = refreshTokenGenerator.Generate();
        var refreshToken = RefreshToken.Create(RefreshTokenId.New(), session.Id,
            refreshTokenHasher.Hash(rawRefreshToken), now);
        sessions.Add(session); sessions.Add(refreshToken);
        AuthorizationSnapshot authorization =
            await authorizationSnapshotProvider.GetAsync(user, cancellationToken);
        AccessToken token = accessTokenProvider.Generate(user, session.Id, authorization);
        audit.Record(
            AuditEventType.LoginSucceeded,
            actorUserId: user.Id,
            targetUserId: user.Id,
            sessionId: session.Id);
        audit.Record(
            AuditEventType.SessionCreated,
            actorUserId: user.Id,
            targetUserId: user.Id,
            sessionId: session.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<LoginResult>.Success(new LoginResult(token.Value, rawRefreshToken, token.ExpiresAtUtc));
    }
}
