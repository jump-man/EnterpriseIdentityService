using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Domain.Auditing;

namespace EnterpriseIdentityService.Infrastructure.Persistence.Repositories;

internal sealed class AuditEntryRepository(ApplicationDbContext dbContext)
    : IAuditEntryRepository
{
    public void Add(AuditEntry entry) => dbContext.AuditEntries.Add(entry);
}
