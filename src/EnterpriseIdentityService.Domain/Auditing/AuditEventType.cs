namespace EnterpriseIdentityService.Domain.Auditing;

public enum AuditEventType
{
    LoginSucceeded = 1,
    LoginFailed = 2,
    SessionCreated = 3,
    SessionRefreshed = 4,
    RefreshTokenReplayDetected = 5,
    Logout = 6,
    LogoutAllDevices = 7,
    PasswordChanged = 8,
    PasswordResetRequested = 9,
    PasswordResetCompleted = 10,
    EmailVerified = 11,
    RoleCreated = 12,
    RoleRenamed = 13,
    RoleEnabled = 14,
    RoleDisabled = 15,
    RoleDeleted = 16,
    RoleAssignedToUser = 17,
    RoleRemovedFromUser = 18,
    PermissionGrantedToRole = 19,
    PermissionRevokedFromRole = 20,
    AuthorizationChangeRejected = 21
}
