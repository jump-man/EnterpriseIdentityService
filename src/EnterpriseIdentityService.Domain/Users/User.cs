using EnterpriseIdentityService.Domain.Abstractions;
using EnterpriseIdentityService.Domain.Users.Events;

namespace EnterpriseIdentityService.Domain.Users;

public sealed class User : AggregateRoot<UserId>
{
    private User(
        UserId id,
        Email email,
        Username username,
        PasswordHash passwordHash,
        DateTimeOffset createdAtUtc)
        : base(id)
    {
        Email = email;
        Username = username;
        PasswordHash = passwordHash;
        Status = UserStatus.Pending;
        CreatedAtUtc = createdAtUtc;
    }

    public Email Email { get; }

    public Username Username { get; }

    public PasswordHash PasswordHash { get; }

    public UserStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset? EmailVerifiedAtUtc { get; private set; }

    public static User Register(
        UserId id,
        Email email,
        Username username,
        PasswordHash passwordHash,
        DateTimeOffset occurredOnUtc)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(passwordHash);

        var user = new User(id, email, username, passwordHash, occurredOnUtc);

        user.RaiseDomainEvent(new UserRegisteredDomainEvent(id, occurredOnUtc));

        return user;
    }

    public void VerifyEmail(DateTimeOffset occurredOnUtc)
    {
        if (Status != UserStatus.Pending)
        {
            throw new InvalidOperationException("Only pending users can verify their email.");
        }

        Status = UserStatus.Active;
        EmailVerifiedAtUtc = occurredOnUtc;

        RaiseDomainEvent(new UserEmailVerifiedDomainEvent(Id, occurredOnUtc));
    }

    public void Disable(DateTimeOffset occurredOnUtc)
    {
        if (Status != UserStatus.Active)
        {
            throw new InvalidOperationException("Only active users can be disabled.");
        }

        Status = UserStatus.Disabled;

        RaiseDomainEvent(new UserDisabledDomainEvent(Id, occurredOnUtc));
    }

    public void Enable(DateTimeOffset occurredOnUtc)
    {
        if (Status != UserStatus.Disabled)
        {
            throw new InvalidOperationException("Only disabled users can be enabled.");
        }

        Status = UserStatus.Active;

        RaiseDomainEvent(new UserEnabledDomainEvent(Id, occurredOnUtc));
    }

    public void Lock(DateTimeOffset occurredOnUtc)
    {
        if (Status != UserStatus.Active)
        {
            throw new InvalidOperationException("Only active users can be locked.");
        }

        Status = UserStatus.Locked;

        RaiseDomainEvent(new UserLockedDomainEvent(Id, occurredOnUtc));
    }

    public void Unlock(DateTimeOffset occurredOnUtc)
    {
        if (Status != UserStatus.Locked)
        {
            throw new InvalidOperationException("Only locked users can be unlocked.");
        }

        Status = UserStatus.Active;

        RaiseDomainEvent(new UserUnlockedDomainEvent(Id, occurredOnUtc));
    }
}
