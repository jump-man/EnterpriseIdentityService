using EnterpriseIdentityService.Application.Abstractions.Authentication;
using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Application.Users.ChangePassword;
using EnterpriseIdentityService.Domain.Users;
using EnterpriseIdentityService.UnitTests.TestDoubles;
using EnterpriseIdentityService.Domain.Auditing;

namespace EnterpriseIdentityService.UnitTests.Application.Users.ChangePassword;

public sealed class ChangePasswordCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldChangePasswordAndRevokeResetTokens()
    {
        User user = ActiveUser();
        var token = PasswordResetToken.Create(PasswordResetTokenId.New(), user.Id, new string('A', 64),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(15));
        var unitOfWork = new FakeUnitOfWork();
        var auditEntries = new FakeAuditEntryRepository();
        var handler = new ChangePasswordCommandHandler(new FakeUsers(user), new FakeTokens(token),
            new FakeHasher("current"), unitOfWork, new FakeSessions(), TestAudit.Create(auditEntries),
            TimeProvider.System);

        var result = await handler.Handle(new ChangePasswordCommand(user.Id, "current", "new"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("new-hash", user.PasswordHash.Value);
        Assert.Equal(1, user.TokenVersion);
        Assert.True(token.IsRevoked);
        Assert.Equal(1, unitOfWork.SaveCalls);
        Assert.Equal(AuditEventType.PasswordChanged, Assert.Single(auditEntries.Entries).EventType);
    }

    [Theory]
    [InlineData("wrong", "new", "ChangePassword.InvalidCurrentPassword")]
    [InlineData("current", "current", "ChangePassword.SamePassword")]
    public async Task Handle_ShouldNotMutate_WhenCredentialCheckFails(string current, string next, string errorCode)
    {
        User user = ActiveUser();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ChangePasswordCommandHandler(new FakeUsers(user), new FakeTokens(),
            new FakeHasher("current"), unitOfWork, new FakeSessions(), TestAudit.Create(),
            TimeProvider.System);
        var result = await handler.Handle(new ChangePasswordCommand(user.Id, current, next), CancellationToken.None);
        Assert.Equal(errorCode, result.Error.Code);
        Assert.Equal(0, user.TokenVersion);
        Assert.Equal(0, unitOfWork.SaveCalls);
    }

    private static User ActiveUser()
    {
        User user = User.Register(UserId.New(), Email.Create("user@example.com"), Username.Create("change.user"),
            PasswordHash.Create("current-hash"), DateTimeOffset.UtcNow);
        user.VerifyEmail(DateTimeOffset.UtcNow);
        user.ClearDomainEvents();
        return user;
    }

    private sealed class FakeUsers(User user) : IUserRepository
    {
        public Task<User?> GetByIdAsync(UserId id, CancellationToken cancellationToken) => Task.FromResult<User?>(user);
        public Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken) => Task.FromResult<User?>(null);
        public Task<bool> ExistsByEmailAsync(Email email, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> ExistsByUsernameAsync(Username username, CancellationToken cancellationToken) => Task.FromResult(false);
        public void Add(User value) { }
    }

    private sealed class FakeTokens(params PasswordResetToken[] values) : IPasswordResetTokenRepository
    {
        public Task<IReadOnlyList<PasswordResetToken>> GetActiveByUserIdAsync(UserId id, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PasswordResetToken>>(values);
        public Task<PasswordResetToken?> GetByHashAsync(string hash, CancellationToken cancellationToken) => Task.FromResult<PasswordResetToken?>(null);
        public void Add(PasswordResetToken token) { }
    }

    private sealed class FakeHasher(string current) : IPasswordHasher
    {
        public PasswordHash Hash(string password) => PasswordHash.Create($"{password}-hash");
        public bool Verify(string password, PasswordHash passwordHash) => password == current;
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCalls { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            return Task.FromResult(1);
        }
    }

    private sealed class FakeSessions : IUserSessionRepository
    {
        public Task<UserSession?> GetByIdAsync(UserSessionId id, CancellationToken ct) => Task.FromResult<UserSession?>(null);
        public Task<RefreshToken?> GetRefreshTokenByHashAsync(string hash, CancellationToken ct) => Task.FromResult<RefreshToken?>(null);
        public Task<IReadOnlyList<UserSession>> GetActiveByUserIdAsync(UserId id, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<UserSession>>([]);
        public Task RevokeAsync(UserSessionId id, UserId userId, DateTimeOffset now, CancellationToken ct) => Task.CompletedTask;
        public void Add(UserSession session) { }
        public void Add(RefreshToken token) { }
    }
}
