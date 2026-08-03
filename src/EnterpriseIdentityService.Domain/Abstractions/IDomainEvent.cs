namespace EnterpriseIdentityService.Domain.Abstractions;

public interface IDomainEvent
{
    DateTimeOffset OccurredOnUtc { get; }
}
