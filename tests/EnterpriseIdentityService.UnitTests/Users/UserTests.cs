using EnterpriseIdentityService.Domain.Users;
using EnterpriseIdentityService.Domain.Users.Events;

namespace EnterpriseIdentityService.UnitTests.Users;

public sealed class UserTests
{
    private static readonly DateTimeOffset OccurredOnUtc =
        new(2026, 8, 3, 8, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Register_ShouldPreserveSuppliedUserId()
    {
        UserId id = UserId.New();

        User user = CreateUser(id: id);

        Assert.Equal(id, user.Id);
    }

    [Fact]
    public void Register_ShouldPreserveSuppliedEmail()
    {
        Email email = Email.Create("user@example.com");

        User user = CreateUser(email: email);

        Assert.Same(email, user.Email);
    }

    [Fact]
    public void Register_ShouldPreserveSuppliedUsername()
    {
        Username username = Username.Create("ali.dev");

        User user = CreateUser(username: username);

        Assert.Same(username, user.Username);
    }

    [Fact]
    public void Register_ShouldPreserveSuppliedPasswordHash()
    {
        PasswordHash passwordHash = PasswordHash.Create("FAKE-HASH");

        User user = CreateUser(passwordHash: passwordHash);

        Assert.Same(passwordHash, user.PasswordHash);
    }

    [Fact]
    public void Register_ShouldSetStatusToPending()
    {
        User user = CreateUser();

        Assert.Equal(UserStatus.Pending, user.Status);
    }

    [Fact]
    public void Register_ShouldSetCreatedAtUtcToSuppliedTime()
    {
        User user = CreateUser(occurredOnUtc: OccurredOnUtc);

        Assert.Equal(OccurredOnUtc, user.CreatedAtUtc);
    }

    [Fact]
    public void Register_ShouldRaiseExactlyOneDomainEvent()
    {
        User user = CreateUser();

        Assert.Single(user.DomainEvents);
    }

    [Fact]
    public void Register_ShouldRaiseUserRegisteredDomainEvent()
    {
        User user = CreateUser();

        Assert.IsType<UserRegisteredDomainEvent>(Assert.Single(user.DomainEvents));
    }

    [Fact]
    public void Register_ShouldPlaceSuppliedUserIdInEvent()
    {
        UserId id = UserId.New();
        User user = CreateUser(id: id);

        var domainEvent = Assert.IsType<UserRegisteredDomainEvent>(Assert.Single(user.DomainEvents));

        Assert.Equal(id, domainEvent.UserId);
    }

    [Fact]
    public void Register_ShouldPlaceSuppliedOccurrenceTimeInEvent()
    {
        User user = CreateUser(occurredOnUtc: OccurredOnUtc);

        var domainEvent = Assert.IsType<UserRegisteredDomainEvent>(Assert.Single(user.DomainEvents));

        Assert.Equal(OccurredOnUtc, domainEvent.OccurredOnUtc);
    }

    [Fact]
    public void Register_ShouldThrowArgumentNullException_WhenEmailIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => User.Register(
            UserId.New(),
            null!,
            Username.Create("ali.dev"),
            PasswordHash.Create("FAKE-HASH"),
            OccurredOnUtc));
    }

    [Fact]
    public void Register_ShouldThrowArgumentNullException_WhenUsernameIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => User.Register(
            UserId.New(),
            Email.Create("user@example.com"),
            null!,
            PasswordHash.Create("FAKE-HASH"),
            OccurredOnUtc));
    }

    [Fact]
    public void Register_ShouldThrowArgumentNullException_WhenPasswordHashIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => User.Register(
            UserId.New(),
            Email.Create("user@example.com"),
            Username.Create("ali.dev"),
            null!,
            OccurredOnUtc));
    }

    private static User CreateUser(
        UserId? id = null,
        Email? email = null,
        Username? username = null,
        PasswordHash? passwordHash = null,
        DateTimeOffset? occurredOnUtc = null)
    {
        return User.Register(
            id ?? UserId.New(),
            email ?? Email.Create("user@example.com"),
            username ?? Username.Create("ali.dev"),
            passwordHash ?? PasswordHash.Create("FAKE-HASH"),
            occurredOnUtc ?? OccurredOnUtc);
    }
}
