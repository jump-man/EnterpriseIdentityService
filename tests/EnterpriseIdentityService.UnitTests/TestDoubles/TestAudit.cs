using EnterpriseIdentityService.Application.Abstractions.Auditing;
using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Application.Auditing;
using EnterpriseIdentityService.Domain.Auditing;

namespace EnterpriseIdentityService.UnitTests.TestDoubles;

internal static class TestAudit
{
    public static AuditRecorder Create(FakeAuditEntryRepository? repository = null) => new(
        repository ?? new FakeAuditEntryRepository(),
        new FakeAuditContextProvider(),
        TimeProvider.System);
}

internal sealed class FakeAuditEntryRepository : IAuditEntryRepository
{
    public List<AuditEntry> Entries { get; } = [];

    public void Add(AuditEntry entry) => Entries.Add(entry);
}

internal sealed class FakeAuditContextProvider : IAuditContextProvider
{
    public AuditContext GetCurrent() => new(
        "unit-test-correlation",
        "127.0.0.1",
        "Unit Test");
}
