using EnterpriseIdentityService.Domain.Abstractions;

namespace EnterpriseIdentityService.Domain.Users.Events;

public sealed record UserLockedDomainEvent(
    UserId UserId,
    DateTimeOffset OccurredOnUtc) : IDomainEvent;
