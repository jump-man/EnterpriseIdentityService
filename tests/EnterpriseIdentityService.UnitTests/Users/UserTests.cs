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

    private static readonly DateTimeOffset EnabledOnUtc =
        new(2026, 8, 3, 11, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset LockedOnUtc =
        new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset UnlockedOnUtc =
        new(2026, 8, 3, 13, 0, 0, TimeSpan.Zero);

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

    [Fact]
    public void Enable_ShouldSetStatusToActive_WhenUserIsDisabled()
    {
        User user = CreateDisabledUserWithoutDomainEvents();

        user.Enable(EnabledOnUtc);

        Assert.Equal(UserStatus.Active, user.Status);
    }

    [Fact]
    public void Enable_ShouldRaiseExactlyOneDomainEvent()
    {
        User user = CreateDisabledUserWithoutDomainEvents();

        user.Enable(EnabledOnUtc);

        Assert.Single(user.DomainEvents);
    }

    [Fact]
    public void Enable_ShouldRaiseUserEnabledDomainEvent()
    {
        User user = CreateDisabledUserWithoutDomainEvents();

        user.Enable(EnabledOnUtc);

        Assert.IsType<UserEnabledDomainEvent>(Assert.Single(user.DomainEvents));
    }

    [Fact]
    public void Enable_ShouldPlaceUserIdInDomainEvent()
    {
        User user = CreateDisabledUserWithoutDomainEvents();

        user.Enable(EnabledOnUtc);

        var domainEvent = Assert.IsType<UserEnabledDomainEvent>(Assert.Single(user.DomainEvents));
        Assert.Equal(user.Id, domainEvent.UserId);
    }

    [Fact]
    public void Enable_ShouldPlaceOccurrenceTimeInDomainEvent()
    {
        User user = CreateDisabledUserWithoutDomainEvents();

        user.Enable(EnabledOnUtc);

        var domainEvent = Assert.IsType<UserEnabledDomainEvent>(Assert.Single(user.DomainEvents));
        Assert.Equal(EnabledOnUtc, domainEvent.OccurredOnUtc);
    }

    [Fact]
    public void Enable_ShouldThrowInvalidOperationException_WhenUserIsPending()
    {
        User user = CreateUser();
        user.ClearDomainEvents();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => user.Enable(EnabledOnUtc));

        Assert.Equal("Only disabled users can be enabled.", exception.Message);
    }

    [Fact]
    public void Enable_ShouldThrowInvalidOperationException_WhenUserIsActive()
    {
        User user = CreateActiveUserWithoutDomainEvents();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => user.Enable(EnabledOnUtc));

        Assert.Equal("Only disabled users can be enabled.", exception.Message);
    }

    [Fact]
    public void Enable_ShouldNotChangeStatus_WhenEnableFails()
    {
        User user = CreateActiveUserWithoutDomainEvents();

        Assert.Throws<InvalidOperationException>(() => user.Enable(EnabledOnUtc));

        Assert.Equal(UserStatus.Active, user.Status);
    }

    [Fact]
    public void Enable_ShouldNotRaiseDomainEvent_WhenEnableFails()
    {
        User user = CreateActiveUserWithoutDomainEvents();

        Assert.Throws<InvalidOperationException>(() => user.Enable(EnabledOnUtc));

        Assert.Empty(user.DomainEvents);
    }

    [Fact]
    public void Lock_ShouldSetStatusToLocked_WhenUserIsActive()
    {
        User user = CreateActiveUserWithoutDomainEvents();

        user.Lock(LockedOnUtc);

        Assert.Equal(UserStatus.Locked, user.Status);
    }

    [Fact]
    public void Lock_ShouldRaiseExactlyOneDomainEvent()
    {
        User user = CreateActiveUserWithoutDomainEvents();

        user.Lock(LockedOnUtc);

        Assert.Single(user.DomainEvents);
    }

    [Fact]
    public void Lock_ShouldRaiseUserLockedDomainEvent()
    {
        User user = CreateActiveUserWithoutDomainEvents();

        user.Lock(LockedOnUtc);

        Assert.IsType<UserLockedDomainEvent>(Assert.Single(user.DomainEvents));
    }

    [Fact]
    public void Lock_ShouldPlaceUserIdInDomainEvent()
    {
        User user = CreateActiveUserWithoutDomainEvents();

        user.Lock(LockedOnUtc);

        var domainEvent = Assert.IsType<UserLockedDomainEvent>(Assert.Single(user.DomainEvents));
        Assert.Equal(user.Id, domainEvent.UserId);
    }

    [Fact]
    public void Lock_ShouldPlaceOccurrenceTimeInDomainEvent()
    {
        User user = CreateActiveUserWithoutDomainEvents();

        user.Lock(LockedOnUtc);

        var domainEvent = Assert.IsType<UserLockedDomainEvent>(Assert.Single(user.DomainEvents));
        Assert.Equal(LockedOnUtc, domainEvent.OccurredOnUtc);
    }

    [Fact]
    public void Lock_ShouldThrowInvalidOperationException_WhenUserIsPending()
    {
        User user = CreateUser();
        user.ClearDomainEvents();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => user.Lock(LockedOnUtc));

        Assert.Equal("Only active users can be locked.", exception.Message);
    }

    [Fact]
    public void Lock_ShouldThrowInvalidOperationException_WhenUserIsAlreadyLocked()
    {
        User user = CreateLockedUserWithoutDomainEvents();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => user.Lock(LockedOnUtc));

        Assert.Equal("Only active users can be locked.", exception.Message);
    }

    [Fact]
    public void Lock_ShouldThrowInvalidOperationException_WhenUserIsDisabled()
    {
        User user = CreateDisabledUserWithoutDomainEvents();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => user.Lock(LockedOnUtc));

        Assert.Equal("Only active users can be locked.", exception.Message);
    }

    [Fact]
    public void Lock_ShouldNotChangeStatus_WhenLockFails()
    {
        User user = CreateLockedUserWithoutDomainEvents();

        Assert.Throws<InvalidOperationException>(() => user.Lock(LockedOnUtc));

        Assert.Equal(UserStatus.Locked, user.Status);
    }

    [Fact]
    public void Lock_ShouldNotRaiseDomainEvent_WhenLockFails()
    {
        User user = CreateLockedUserWithoutDomainEvents();

        Assert.Throws<InvalidOperationException>(() => user.Lock(LockedOnUtc));

        Assert.Empty(user.DomainEvents);
    }

    [Fact]
    public void Unlock_ShouldSetStatusToActive_WhenUserIsLocked()
    {
        User user = CreateLockedUserWithoutDomainEvents();

        user.Unlock(UnlockedOnUtc);

        Assert.Equal(UserStatus.Active, user.Status);
    }

    [Fact]
    public void Unlock_ShouldRaiseExactlyOneDomainEvent()
    {
        User user = CreateLockedUserWithoutDomainEvents();

        user.Unlock(UnlockedOnUtc);

        Assert.Single(user.DomainEvents);
    }

    [Fact]
    public void Unlock_ShouldRaiseUserUnlockedDomainEvent()
    {
        User user = CreateLockedUserWithoutDomainEvents();

        user.Unlock(UnlockedOnUtc);

        Assert.IsType<UserUnlockedDomainEvent>(Assert.Single(user.DomainEvents));
    }

    [Fact]
    public void Unlock_ShouldPlaceUserIdInDomainEvent()
    {
        User user = CreateLockedUserWithoutDomainEvents();

        user.Unlock(UnlockedOnUtc);

        var domainEvent = Assert.IsType<UserUnlockedDomainEvent>(Assert.Single(user.DomainEvents));
        Assert.Equal(user.Id, domainEvent.UserId);
    }

    [Fact]
    public void Unlock_ShouldPlaceOccurrenceTimeInDomainEvent()
    {
        User user = CreateLockedUserWithoutDomainEvents();

        user.Unlock(UnlockedOnUtc);

        var domainEvent = Assert.IsType<UserUnlockedDomainEvent>(Assert.Single(user.DomainEvents));
        Assert.Equal(UnlockedOnUtc, domainEvent.OccurredOnUtc);
    }

    [Fact]
    public void Unlock_ShouldThrowInvalidOperationException_WhenUserIsPending()
    {
        User user = CreateUser();
        user.ClearDomainEvents();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => user.Unlock(UnlockedOnUtc));

        Assert.Equal("Only locked users can be unlocked.", exception.Message);
    }

    [Fact]
    public void Unlock_ShouldThrowInvalidOperationException_WhenUserIsActive()
    {
        User user = CreateActiveUserWithoutDomainEvents();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => user.Unlock(UnlockedOnUtc));

        Assert.Equal("Only locked users can be unlocked.", exception.Message);
    }

    [Fact]
    public void Unlock_ShouldThrowInvalidOperationException_WhenUserIsDisabled()
    {
        User user = CreateDisabledUserWithoutDomainEvents();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => user.Unlock(UnlockedOnUtc));

        Assert.Equal("Only locked users can be unlocked.", exception.Message);
    }

    [Fact]
    public void Unlock_ShouldNotChangeStatus_WhenUnlockFails()
    {
        User user = CreateActiveUserWithoutDomainEvents();

        Assert.Throws<InvalidOperationException>(() => user.Unlock(UnlockedOnUtc));

        Assert.Equal(UserStatus.Active, user.Status);
    }

    [Fact]
    public void Unlock_ShouldNotRaiseDomainEvent_WhenUnlockFails()
    {
        User user = CreateActiveUserWithoutDomainEvents();

        Assert.Throws<InvalidOperationException>(() => user.Unlock(UnlockedOnUtc));

        Assert.Empty(user.DomainEvents);
    }

    [Fact]
    public void Disable_ShouldThrowInvalidOperationException_WhenUserIsLocked()
    {
        User user = CreateLockedUserWithoutDomainEvents();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => user.Disable(DisabledOnUtc));

        Assert.Equal("Only active users can be disabled.", exception.Message);
        Assert.Equal(UserStatus.Locked, user.Status);
        Assert.Empty(user.DomainEvents);
    }

    [Fact]
    public void Enable_ShouldThrowInvalidOperationException_WhenUserIsLocked()
    {
        User user = CreateLockedUserWithoutDomainEvents();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => user.Enable(EnabledOnUtc));

        Assert.Equal("Only disabled users can be enabled.", exception.Message);
        Assert.Equal(UserStatus.Locked, user.Status);
        Assert.Empty(user.DomainEvents);
    }

    [Fact]
    public void VerifyEmail_ShouldThrowInvalidOperationException_WhenUserIsLocked()
    {
        User user = CreateLockedUserWithoutDomainEvents();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => user.VerifyEmail(EmailVerifiedOnUtc));

        Assert.Equal("Only pending users can verify their email.", exception.Message);
        Assert.Equal(UserStatus.Locked, user.Status);
        Assert.Empty(user.DomainEvents);
    }

    [Fact]
    public void ResetPassword_ShouldReplaceHashIncrementTokenVersionAndKeepActiveStatus()
    {
        User user = CreateActiveUserWithoutDomainEvents();
        PasswordHash hash = PasswordHash.Create("NEW-HASH");

        user.ResetPassword(hash, OccurredOnUtc);

        Assert.Same(hash, user.PasswordHash);
        Assert.Equal(1, user.TokenVersion);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.IsType<UserPasswordChangedDomainEvent>(Assert.Single(user.DomainEvents));
    }

    [Fact]
    public void ResetPassword_ShouldKeepLockedUserLocked()
    {
        User user = CreateLockedUserWithoutDomainEvents();
        user.ResetPassword(PasswordHash.Create("NEW-HASH"), OccurredOnUtc);
        Assert.Equal(UserStatus.Locked, user.Status);
        Assert.Equal(1, user.TokenVersion);
    }

    [Fact]
    public void ResetPassword_ShouldRejectPendingAndDisabledUsers()
    {
        Assert.Throws<InvalidOperationException>(() =>
            CreateUser().ResetPassword(PasswordHash.Create("NEW-HASH"), OccurredOnUtc));
        Assert.Throws<InvalidOperationException>(() =>
            CreateDisabledUserWithoutDomainEvents().ResetPassword(PasswordHash.Create("NEW-HASH"), OccurredOnUtc));
    }

    [Fact]
    public void ChangePassword_ShouldReplaceHashIncrementVersionAndRaiseExistingEvent()
    {
        User user = CreateActiveUserWithoutDomainEvents();
        PasswordHash hash = PasswordHash.Create("CHANGED-HASH");
        user.ChangePassword(hash, OccurredOnUtc);
        Assert.Same(hash, user.PasswordHash);
        Assert.Equal(1, user.TokenVersion);
        Assert.IsType<UserPasswordChangedDomainEvent>(Assert.Single(user.DomainEvents));
    }

    [Fact]
    public void ChangePassword_ShouldRejectNonActiveStatesWithoutMutation()
    {
        User pending = CreateUser();
        pending.ClearDomainEvents();
        User[] users = [pending, CreateLockedUserWithoutDomainEvents(), CreateDisabledUserWithoutDomainEvents()];
        foreach (User user in users)
        {
            PasswordHash original = user.PasswordHash;
            Assert.Throws<InvalidOperationException>(() =>
                user.ChangePassword(PasswordHash.Create("CHANGED-HASH"), OccurredOnUtc));
            Assert.Same(original, user.PasswordHash);
            Assert.Equal(0, user.TokenVersion);
            Assert.Empty(user.DomainEvents);
        }
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

    private static User CreateLockedUserWithoutDomainEvents()
    {
        User user = CreateActiveUserWithoutDomainEvents();
        user.Lock(LockedOnUtc);
        user.ClearDomainEvents();

        return user;
    }
}
