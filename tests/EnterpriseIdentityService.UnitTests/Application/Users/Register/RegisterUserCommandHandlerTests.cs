using EnterpriseIdentityService.Application.Abstractions.Authentication;
using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Application.Users.Register;
using EnterpriseIdentityService.Domain.Users;
using EnterpriseIdentityService.Domain.Users.Events;

namespace EnterpriseIdentityService.UnitTests.Application.Users.Register;

public sealed class RegisterUserCommandHandlerTests
{
    private const string PlainTextPassword = "plain-text-password";
    private const string HashedPassword = "HASHED-PASSWORD";

    [Fact]
    public async Task Handle_ShouldRegisterAndPersistUser_WhenCommandIsValid()
    {
        var repository = new FakeUserRepository();
        var passwordHasher = new FakePasswordHasher();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new RegisterUserCommandHandler(repository, passwordHasher, unitOfWork);
        var command = new RegisterUserCommand(
            "  USER@Example.com ",
            "  ali.dev  ",
            PlainTextPassword);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        User user = Assert.Single(repository.AddedUsers);
        Assert.Equal(user.Id, result.Value);
        Assert.Equal("user@example.com", Assert.Single(repository.CheckedEmails).Value);
        Assert.Equal("ali.dev", Assert.Single(repository.CheckedUsernames).Value);
        Assert.Equal(PlainTextPassword, Assert.Single(passwordHasher.Passwords));
        Assert.Equal("user@example.com", user.Email.Value);
        Assert.Equal("ali.dev", user.Username.Value);
        Assert.Equal(HashedPassword, user.PasswordHash.Value);
        Assert.NotEqual(PlainTextPassword, user.PasswordHash.Value);
        var domainEvent = Assert.IsType<UserRegisteredDomainEvent>(Assert.Single(user.DomainEvents));
        Assert.Equal(user.Id, domainEvent.UserId);
        Assert.Equal(user.CreatedAtUtc, domainEvent.OccurredOnUtc);
        Assert.Equal(1, unitOfWork.CallCount);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmailAlreadyInUse_WhenEmailExists()
    {
        var repository = new FakeUserRepository { EmailExists = true };
        var passwordHasher = new FakePasswordHasher();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new RegisterUserCommandHandler(repository, passwordHasher, unitOfWork);

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.Equal(RegisterUserErrors.EmailAlreadyInUse, result.Error);
        Assert.Empty(repository.CheckedUsernames);
        AssertNoWriteOccurred(repository, passwordHasher, unitOfWork);
    }

    [Fact]
    public async Task Handle_ShouldReturnUsernameAlreadyInUse_WhenUsernameExists()
    {
        var repository = new FakeUserRepository { UsernameExists = true };
        var passwordHasher = new FakePasswordHasher();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new RegisterUserCommandHandler(repository, passwordHasher, unitOfWork);

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.Equal(RegisterUserErrors.UsernameAlreadyInUse, result.Error);
        Assert.Single(repository.CheckedEmails);
        Assert.Single(repository.CheckedUsernames);
        AssertNoWriteOccurred(repository, passwordHasher, unitOfWork);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_ShouldReturnEmailRequired_WhenEmailIsMissing(string? email)
    {
        var fixture = new HandlerFixture();

        var result = await fixture.Handler.Handle(
            new RegisterUserCommand(email!, "ali.dev", PlainTextPassword),
            CancellationToken.None);

        Assert.Equal(RegisterUserErrors.EmailRequired, result.Error);
        fixture.AssertNoDependencyWasCalled();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_ShouldReturnUsernameRequired_WhenUsernameIsMissing(string? username)
    {
        var fixture = new HandlerFixture();

        var result = await fixture.Handler.Handle(
            new RegisterUserCommand("user@example.com", username!, PlainTextPassword),
            CancellationToken.None);

        Assert.Equal(RegisterUserErrors.UsernameRequired, result.Error);
        fixture.AssertNoDependencyWasCalled();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_ShouldReturnPasswordRequired_WhenPasswordIsMissing(string? password)
    {
        var fixture = new HandlerFixture();

        var result = await fixture.Handler.Handle(
            new RegisterUserCommand("user@example.com", "ali.dev", password!),
            CancellationToken.None);

        Assert.Equal(RegisterUserErrors.PasswordRequired, result.Error);
        fixture.AssertNoDependencyWasCalled();
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("user@@example.com")]
    public async Task Handle_ShouldReturnInvalidEmail_WhenEmailIsInvalid(string email)
    {
        var fixture = new HandlerFixture();

        var result = await fixture.Handler.Handle(
            new RegisterUserCommand(email, "ali.dev", PlainTextPassword),
            CancellationToken.None);

        Assert.Equal(RegisterUserErrors.InvalidEmail, result.Error);
        fixture.AssertNoDependencyWasCalled();
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("invalid username")]
    public async Task Handle_ShouldReturnInvalidUsername_WhenUsernameIsInvalid(string username)
    {
        var fixture = new HandlerFixture();

        var result = await fixture.Handler.Handle(
            new RegisterUserCommand("user@example.com", username, PlainTextPassword),
            CancellationToken.None);

        Assert.Equal(RegisterUserErrors.InvalidUsername, result.Error);
        fixture.AssertNoDependencyWasCalled();
    }

    [Fact]
    public async Task Handle_ShouldForwardCancellationToken()
    {
        var fixture = new HandlerFixture();
        using var cancellationTokenSource = new CancellationTokenSource();
        CancellationToken cancellationToken = cancellationTokenSource.Token;

        var result = await fixture.Handler.Handle(ValidCommand(), cancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(cancellationToken, fixture.Repository.EmailCancellationToken);
        Assert.Equal(cancellationToken, fixture.Repository.UsernameCancellationToken);
        Assert.Equal(cancellationToken, fixture.UnitOfWork.CancellationToken);
    }

    private static RegisterUserCommand ValidCommand() =>
        new("user@example.com", "ali.dev", PlainTextPassword);

    private static void AssertNoWriteOccurred(
        FakeUserRepository repository,
        FakePasswordHasher passwordHasher,
        FakeUnitOfWork unitOfWork)
    {
        Assert.Empty(passwordHasher.Passwords);
        Assert.Empty(repository.AddedUsers);
        Assert.Equal(0, unitOfWork.CallCount);
    }

    private sealed class HandlerFixture
    {
        public HandlerFixture()
        {
            Handler = new RegisterUserCommandHandler(Repository, PasswordHasher, UnitOfWork);
        }

        public FakeUserRepository Repository { get; } = new();

        public FakePasswordHasher PasswordHasher { get; } = new();

        public FakeUnitOfWork UnitOfWork { get; } = new();

        public RegisterUserCommandHandler Handler { get; }

        public void AssertNoDependencyWasCalled()
        {
            Assert.Empty(Repository.CheckedEmails);
            Assert.Empty(Repository.CheckedUsernames);
            AssertNoWriteOccurred(Repository, PasswordHasher, UnitOfWork);
        }
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken) =>
            Task.FromResult<User?>(null);

        public Task<User?> GetByIdAsync(UserId userId, CancellationToken cancellationToken) =>
            Task.FromResult<User?>(null);

        public bool EmailExists { get; init; }

        public bool UsernameExists { get; init; }

        public List<Email> CheckedEmails { get; } = [];

        public List<Username> CheckedUsernames { get; } = [];

        public List<User> AddedUsers { get; } = [];

        public CancellationToken EmailCancellationToken { get; private set; }

        public CancellationToken UsernameCancellationToken { get; private set; }

        public Task<bool> ExistsByEmailAsync(Email email, CancellationToken cancellationToken)
        {
            CheckedEmails.Add(email);
            EmailCancellationToken = cancellationToken;
            return Task.FromResult(EmailExists);
        }

        public Task<bool> ExistsByUsernameAsync(Username username, CancellationToken cancellationToken)
        {
            CheckedUsernames.Add(username);
            UsernameCancellationToken = cancellationToken;
            return Task.FromResult(UsernameExists);
        }

        public void Add(User user) => AddedUsers.Add(user);
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public List<string> Passwords { get; } = [];

        public PasswordHash Hash(string password)
        {
            Passwords.Add(password);
            return PasswordHash.Create(HashedPassword);
        }

        public bool Verify(string password, PasswordHash passwordHash) => false;
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int CallCount { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            CancellationToken = cancellationToken;
            return Task.FromResult(1);
        }
    }
}
