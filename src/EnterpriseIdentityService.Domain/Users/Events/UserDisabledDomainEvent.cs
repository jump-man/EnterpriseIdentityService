using EnterpriseIdentityService.Domain.Abstractions;

namespace EnterpriseIdentityService.Domain.Users.Events;

public sealed record UserDisabledDomainEvent(
    UserId UserId,
    DateTimeOffset OccurredOnUtc) : IDomainEvent;
