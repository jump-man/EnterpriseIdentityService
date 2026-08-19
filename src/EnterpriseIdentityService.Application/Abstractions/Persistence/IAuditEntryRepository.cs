using EnterpriseIdentityService.Domain.Auditing;

namespace EnterpriseIdentityService.Application.Abstractions.Persistence;

public interface IAuditEntryRepository
{
    void Add(AuditEntry entry);
}
