using EnterpriseIdentityService.Domain.Abstractions;

namespace EnterpriseIdentityService.Domain.Users.Events;

public sealed record UserEmailVerifiedDomainEvent(
    UserId UserId,
    DateTimeOffset OccurredOnUtc) : IDomainEvent;
