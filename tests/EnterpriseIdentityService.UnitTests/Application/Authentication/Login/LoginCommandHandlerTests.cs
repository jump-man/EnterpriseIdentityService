using EnterpriseIdentityService.Application.Abstractions.Authentication;
using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Application.Authentication.Login;
using EnterpriseIdentityService.Application.Authentication;
using EnterpriseIdentityService.Domain.Users;
using Microsoft.Extensions.Options;
using EnterpriseIdentityService.Application.Abstractions.Authorization;
using EnterpriseIdentityService.Application.Authorization;

namespace EnterpriseIdentityService.UnitTests.Application.Authentication.Login;

public sealed class LoginCommandHandlerTests
{
    private const string Password = "correct-password";

    [Fact]
    public async Task Handle_ShouldReturnToken_WhenCredentialsBelongToActiveUser()
    {
        User user = ActiveUser();
        var repository = new FakeUserRepository(user);
        var passwordHasher = new FakePasswordHasher(true);
        var tokenProvider = new FakeAccessTokenProvider();
        var handler = CreateHandler(repository, passwordHasher, tokenProvider);

        var result = await handler.Handle(
            new LoginCommand(" USER@example.com ", Password),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("access-token", result.Value.AccessToken);
        Assert.Equal(tokenProvider.Token.ExpiresAtUtc, result.Value.ExpiresAtUtc);
        Assert.Equal("user@example.com", Assert.Single(repository.Emails).Value);
        Assert.Equal(Password, passwordHasher.Password);
        Assert.Equal(user.PasswordHash, passwordHasher.PasswordHash);
        Assert.Same(user, Assert.Single(tokenProvider.Users));
    }

    [Fact]
    public async Task Handle_ShouldReturnGenericError_WhenUserDoesNotExist()
    {
        var fixture = new Fixture(null, true);

        var result = await fixture.Handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.Equal(LoginErrors.InvalidCredentials, result.Error);
        Assert.Null(fixture.PasswordHasher.Password);
        Assert.Empty(fixture.TokenProvider.Users);
    }

    [Fact]
    public async Task Handle_ShouldReturnSameGenericError_WhenPasswordIsIncorrect()
    {
        var fixture = new Fixture(ActiveUser(), false);

        var result = await fixture.Handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.Equal(LoginErrors.InvalidCredentials, result.Error);
        Assert.Empty(fixture.TokenProvider.Users);
    }

    [Theory]
    [InlineData(UserStatus.Pending)]
    [InlineData(UserStatus.Locked)]
    [InlineData(UserStatus.Disabled)]
    public async Task Handle_ShouldReturnGenericError_WhenUserIsNotActive(UserStatus status)
    {
        User user = User.Register(
            UserId.New(), Email.Create("user@example.com"), Username.Create("ali.dev"),
            PasswordHash.Create("stored-hash"), DateTimeOffset.UtcNow);
        if (status != UserStatus.Pending)
        {
            user.VerifyEmail(DateTimeOffset.UtcNow);
            if (status == UserStatus.Locked)
            {
                user.Lock(DateTimeOffset.UtcNow);
            }
            else
            {
                user.Disable(DateTimeOffset.UtcNow);
            }
        }

        var fixture = new Fixture(user, true);
        var result = await fixture.Handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.Equal(LoginErrors.InvalidCredentials, result.Error);
        Assert.Empty(fixture.TokenProvider.Users);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    public async Task Handle_ShouldRejectInvalidEmailBeforeCallingDependencies(string email)
    {
        var fixture = new Fixture(ActiveUser(), true);

        var result = await fixture.Handler.Handle(
            new LoginCommand(email, Password), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(fixture.Repository.Emails);
        Assert.Null(fixture.PasswordHasher.Password);
        Assert.Empty(fixture.TokenProvider.Users);
    }

    private static LoginCommand ValidCommand() => new("user@example.com", Password);

    private static LoginCommandHandler CreateHandler(IUserRepository users, IPasswordHasher passwords,
        IAccessTokenProvider accessTokens) => new(users, passwords, accessTokens, new FakeSessions(),
            new FakeRefreshGenerator(), new FakeRefreshHasher(), new FakeAuthorizationSnapshots(),
            new FakeUnitOfWork(), TimeProvider.System,
            Options.Create(new AuthenticationSessionOptions { Lifetime = TimeSpan.FromDays(30) }));

    private static User ActiveUser()
    {
        User user = User.Register(
            UserId.New(), Email.Create("user@example.com"), Username.Create("ali.dev"),
            PasswordHash.Create("stored-hash"), DateTimeOffset.UtcNow);
        user.VerifyEmail(DateTimeOffset.UtcNow);
        return user;
    }

    private sealed class Fixture(User? user, bool passwordMatches)
    {
        public FakeUserRepository Repository { get; } = new(user);
        public FakePasswordHasher PasswordHasher { get; } = new(passwordMatches);
        public FakeAccessTokenProvider TokenProvider { get; } = new();
        public LoginCommandHandler Handler => CreateHandler(Repository, PasswordHasher, TokenProvider);
    }

    private sealed class FakeUserRepository(User? user) : IUserRepository
    {
        public List<Email> Emails { get; } = [];
        public Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken)
        {
            Emails.Add(email);
            return Task.FromResult(user);
        }
        public Task<User?> GetByIdAsync(UserId userId, CancellationToken cancellationToken) =>
            Task.FromResult<User?>(null);
        public Task<bool> ExistsByEmailAsync(Email email, CancellationToken cancellationToken) =>
            Task.FromResult(false);
        public Task<bool> ExistsByUsernameAsync(Username username, CancellationToken cancellationToken) =>
            Task.FromResult(false);
        public void Add(User addedUser) { }
    }

    private sealed class FakePasswordHasher(bool matches) : IPasswordHasher
    {
        public string? Password { get; private set; }
        public PasswordHash? PasswordHash { get; private set; }
        public PasswordHash Hash(string password) => PasswordHash.Create("hash");
        public bool Verify(string password, PasswordHash passwordHash)
        {
            Password = password;
            PasswordHash = passwordHash;
            return matches;
        }
    }

    private sealed class FakeAccessTokenProvider : IAccessTokenProvider
    {
        public AccessToken Token { get; } = new(
            "access-token", DateTimeOffset.UtcNow.AddMinutes(15));
        public List<User> Users { get; } = [];
        public AccessToken Generate(
            User user,
            UserSessionId sessionId,
            AuthorizationSnapshot authorization)
        {
            Users.Add(user);
            return Token;
        }
    }

    private sealed class FakeAuthorizationSnapshots : IAuthorizationSnapshotProvider
    {
        public Task<AuthorizationSnapshot> GetAsync(User user, CancellationToken cancellationToken) =>
            Task.FromResult(new AuthorizationSnapshot(user.AuthorizationVersion, []));
    }

    private sealed class FakeRefreshGenerator : IRefreshTokenGenerator { public string Generate() => new('R', 43); }
    private sealed class FakeRefreshHasher : IRefreshTokenHasher { public string Hash(string rawToken) => new('A', 64); }
    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }
    private sealed class FakeSessions : IUserSessionRepository
    {
        public Task<UserSession?> GetByIdAsync(UserSessionId id, CancellationToken ct) => Task.FromResult<UserSession?>(null);
        public Task<RefreshToken?> GetRefreshTokenByHashAsync(string hash, CancellationToken ct) => Task.FromResult<RefreshToken?>(null);
        public Task<IReadOnlyList<UserSession>> GetActiveByUserIdAsync(UserId id, CancellationToken ct) => Task.FromResult<IReadOnlyList<UserSession>>([]);
        public Task RevokeAsync(UserSessionId id, UserId userId, DateTimeOffset now, CancellationToken ct) => Task.CompletedTask;
        public void Add(UserSession session) { }
        public void Add(RefreshToken token) { }
    }
}
