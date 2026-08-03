using EnterpriseIdentityService.Domain.Abstractions;

namespace EnterpriseIdentityService.Domain.Users.Events;

public sealed record UserUnlockedDomainEvent(
    UserId UserId,
    DateTimeOffset OccurredOnUtc) : IDomainEvent;
