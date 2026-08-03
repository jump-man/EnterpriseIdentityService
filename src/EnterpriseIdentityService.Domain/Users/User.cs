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

    public UserStatus Status { get; }

    public DateTimeOffset CreatedAtUtc { get; }

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
}
