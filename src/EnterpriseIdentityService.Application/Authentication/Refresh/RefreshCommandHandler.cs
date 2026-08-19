using EnterpriseIdentityService.Application.Abstractions;
using EnterpriseIdentityService.Application.Abstractions.Authentication;
using EnterpriseIdentityService.Application.Abstractions.Messaging;
using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Domain.Users;
using EnterpriseIdentityService.Application.Abstractions.Authorization;
using EnterpriseIdentityService.Application.Authorization;

namespace EnterpriseIdentityService.Application.Authentication.Refresh;
public sealed class RefreshCommandHandler(IUserSessionRepository sessions, IUserRepository users,
    IRefreshTokenHasher hasher, IRefreshTokenGenerator generator, IAccessTokenProvider accessTokens,
    IAuthorizationSnapshotProvider authorizationSnapshots, IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<RefreshCommand, AuthenticationTokensResult>
{
    public async Task<Result<AuthenticationTokensResult>> Handle(RefreshCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken)) return Result<AuthenticationTokensResult>.Failure(RefreshErrors.TokenRequired);
        if (command.RefreshToken.Length != 43 || command.RefreshToken.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_'))
            return Result<AuthenticationTokensResult>.Failure(RefreshErrors.InvalidToken);
        RefreshToken? token = await sessions.GetRefreshTokenByHashAsync(hasher.Hash(command.RefreshToken), cancellationToken);
        if (token is null) return Result<AuthenticationTokensResult>.Failure(RefreshErrors.InvalidToken);
        UserSession? session = await sessions.GetByIdAsync(token.SessionId, cancellationToken);
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (session is null) return Result<AuthenticationTokensResult>.Failure(RefreshErrors.InvalidToken);
        if (token.IsConsumed)
        {
            session.Revoke(now);
            try { await unitOfWork.SaveChangesAsync(cancellationToken); } catch (ConcurrencyException) { }
            return Result<AuthenticationTokensResult>.Failure(RefreshErrors.InvalidToken);
        }
        User? user = await users.GetByIdAsync(session.UserId, cancellationToken);
        if (user is null || user.Status != UserStatus.Active || !session.IsUsable(now) ||
            session.TokenVersionAtIssue != user.TokenVersion)
            return Result<AuthenticationTokensResult>.Failure(RefreshErrors.InvalidToken);
        token.Consume(now); session.RecordUse(now);
        string raw = generator.Generate();
        sessions.Add(RefreshToken.Create(RefreshTokenId.New(), session.Id, hasher.Hash(raw), now));
        AuthorizationSnapshot authorization = await authorizationSnapshots.GetAsync(user, cancellationToken);
        AccessToken access = accessTokens.Generate(user, session.Id, authorization);
        try { await unitOfWork.SaveChangesAsync(cancellationToken); }
        catch (ConcurrencyException) { return Result<AuthenticationTokensResult>.Failure(RefreshErrors.InvalidToken); }
        return Result<AuthenticationTokensResult>.Success(new(access.Value, raw, access.ExpiresAtUtc));
    }
}
