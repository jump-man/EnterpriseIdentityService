namespace EnterpriseIdentityService.Application.Abstractions.Auditing;

public interface IAuditContextProvider
{
    AuditContext GetCurrent();
}
