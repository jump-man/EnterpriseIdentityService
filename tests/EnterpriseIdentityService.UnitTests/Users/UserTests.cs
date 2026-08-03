using EnterpriseIdentityService.Domain.Users;
using EnterpriseIdentityService.Domain.Users.Events;

namespace EnterpriseIdentityService.UnitTests.Users;

public sealed class UserTests
{
    private static readonly DateTimeOffset OccurredOnUtc =
        new(2026, 8, 3, 8, 30, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset EmailVerifiedOnUtc =
        new(2026, 8, 3, 9, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset DisabledOnUtc =
        new(2026, 8, 3, 10, 0, 0, TimeSpan.Zero);

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

    [Fact]
    public void VerifyEmail_ShouldSetStatusToActive_WhenUserIsPending()
    {
        User user = CreateUser();

        user.VerifyEmail(EmailVerifiedOnUtc);

        Assert.Equal(UserStatus.Active, user.Status);
    }

    [Fact]
    public void VerifyEmail_ShouldRaiseExactlyOneDomainEvent()
    {
        User user = CreateUser();
        user.ClearDomainEvents();

        user.VerifyEmail(EmailVerifiedOnUtc);

        Assert.Single(user.DomainEvents);
    }

    [Fact]
    public void VerifyEmail_ShouldRaiseUserEmailVerifiedDomainEvent()
    {
        User user = CreateUser();
        user.ClearDomainEvents();

        user.VerifyEmail(EmailVerifiedOnUtc);

        Assert.IsType<UserEmailVerifiedDomainEvent>(Assert.Single(user.DomainEvents));
    }

    [Fact]
    public void VerifyEmail_ShouldPlaceUserIdInDomainEvent()
    {
        User user = CreateUser();
        user.ClearDomainEvents();

        user.VerifyEmail(EmailVerifiedOnUtc);

        var domainEvent = Assert.IsType<UserEmailVerifiedDomainEvent>(Assert.Single(user.DomainEvents));
        Assert.Equal(user.Id, domainEvent.UserId);
    }

    [Fact]
    public void VerifyEmail_ShouldPlaceOccurrenceTimeInDomainEvent()
    {
        User user = CreateUser();
        user.ClearDomainEvents();

        user.VerifyEmail(EmailVerifiedOnUtc);

        var domainEvent = Assert.IsType<UserEmailVerifiedDomainEvent>(Assert.Single(user.DomainEvents));
        Assert.Equal(EmailVerifiedOnUtc, domainEvent.OccurredOnUtc);
    }

    [Fact]
    public void VerifyEmail_ShouldThrowInvalidOperationException_WhenUserIsAlreadyActive()
    {
        User user = CreateActiveUserWithoutDomainEvents();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => user.VerifyEmail(EmailVerifiedOnUtc));

        Assert.Equal("Only pending users can verify their email.", exception.Message);
    }

    [Fact]
    public void VerifyEmail_ShouldNotChangeStatus_WhenVerificationFails()
    {
        User user = CreateActiveUserWithoutDomainEvents();

        Assert.Throws<InvalidOperationException>(() => user.VerifyEmail(EmailVerifiedOnUtc));

        Assert.Equal(UserStatus.Active, user.Status);
    }

    [Fact]
    public void VerifyEmail_ShouldNotRaiseDomainEvent_WhenVerificationFails()
    {
        User user = CreateActiveUserWithoutDomainEvents();

        Assert.Throws<InvalidOperationException>(() => user.VerifyEmail(EmailVerifiedOnUtc));

        Assert.Empty(user.DomainEvents);
    }

    [Fact]
    public void Disable_ShouldSetStatusToDisabled_WhenUserIsActive()
    {
        User user = CreateActiveUserWithoutDomainEvents();

        user.Disable(DisabledOnUtc);

        Assert.Equal(UserStatus.Disabled, user.Status);
    }

    [Fact]
    public void Disable_ShouldRaiseExactlyOneDomainEvent()
    {
        User user = CreateActiveUserWithoutDomainEvents();

        user.Disable(DisabledOnUtc);

        Assert.Single(user.DomainEvents);
    }

    [Fact]
    public void Disable_ShouldRaiseUserDisabledDomainEvent()
    {
        User user = CreateActiveUserWithoutDomainEvents();

        user.Disable(DisabledOnUtc);

        Assert.IsType<UserDisabledDomainEvent>(Assert.Single(user.DomainEvents));
    }

    [Fact]
    public void Disable_ShouldPlaceUserIdInDomainEvent()
    {
        User user = CreateActiveUserWithoutDomainEvents();

        user.Disable(DisabledOnUtc);

        var domainEvent = Assert.IsType<UserDisabledDomainEvent>(Assert.Single(user.DomainEvents));
        Assert.Equal(user.Id, domainEvent.UserId);
    }

    [Fact]
    public void Disable_ShouldPlaceOccurrenceTimeInDomainEvent()
    {
        User user = CreateActiveUserWithoutDomainEvents();

        user.Disable(DisabledOnUtc);

        var domainEvent = Assert.IsType<UserDisabledDomainEvent>(Assert.Single(user.DomainEvents));
        Assert.Equal(DisabledOnUtc, domainEvent.OccurredOnUtc);
    }

    [Fact]
    public void Disable_ShouldThrowInvalidOperationException_WhenUserIsPending()
    {
        User user = CreateUser();
        user.ClearDomainEvents();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => user.Disable(DisabledOnUtc));

        Assert.Equal("Only active users can be disabled.", exception.Message);
    }

    [Fact]
    public void Disable_ShouldThrowInvalidOperationException_WhenUserIsAlreadyDisabled()
    {
        User user = CreateDisabledUserWithoutDomainEvents();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => user.Disable(DisabledOnUtc));

        Assert.Equal("Only active users can be disabled.", exception.Message);
    }

    [Fact]
    public void Disable_ShouldNotChangeStatus_WhenDisableFails()
    {
        User user = CreateDisabledUserWithoutDomainEvents();

        Assert.Throws<InvalidOperationException>(() => user.Disable(DisabledOnUtc));

        Assert.Equal(UserStatus.Disabled, user.Status);
    }

    [Fact]
    public void Disable_ShouldNotRaiseDomainEvent_WhenDisableFails()
    {
        User user = CreateDisabledUserWithoutDomainEvents();

        Assert.Throws<InvalidOperationException>(() => user.Disable(DisabledOnUtc));

        Assert.Empty(user.DomainEvents);
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

    private static User CreateActiveUserWithoutDomainEvents()
    {
        User user = CreateUser();
        user.ClearDomainEvents();
        user.VerifyEmail(EmailVerifiedOnUtc);
        user.ClearDomainEvents();

        return user;
    }

    private static User CreateDisabledUserWithoutDomainEvents()
    {
        User user = CreateActiveUserWithoutDomainEvents();
        user.Disable(DisabledOnUtc);
        user.ClearDomainEvents();

        return user;
    }
}
