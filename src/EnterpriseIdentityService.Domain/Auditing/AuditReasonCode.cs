namespace EnterpriseIdentityService.Domain.Auditing;

public enum AuditReasonCode
{
    InvalidCredentials = 1,
    RefreshTokenReplay = 2,
    GrantCeilingViolation = 3,
    LastAdministratorProtection = 4,
    ProtectedSystemRole = 5
}
