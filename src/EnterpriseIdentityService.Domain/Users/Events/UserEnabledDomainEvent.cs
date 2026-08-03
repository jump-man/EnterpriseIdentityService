using EnterpriseIdentityService.Domain.Abstractions;

namespace EnterpriseIdentityService.Domain.Users.Events;

public sealed record UserEnabledDomainEvent(
    UserId UserId,
    DateTimeOffset OccurredOnUtc) : IDomainEvent;
