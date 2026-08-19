using EnterpriseIdentityService.Application.Abstractions;

namespace EnterpriseIdentityService.Application.Authorization;

public static class AuthorizationErrors
{
    public static readonly Error InvalidRoleName = new(
        "Authorization.InvalidRoleName", "The role name is invalid.");
    public static readonly Error RoleNotFound = new(
        "Authorization.RoleNotFound", "The role was not found.");
    public static readonly Error UserNotFound = new(
        "Authorization.UserNotFound", "The user was not found.");
    public static readonly Error RoleAlreadyExists = new(
        "Authorization.RoleAlreadyExists", "A role with this name already exists.");
    public static readonly Error SystemRoleProtected = new(
        "Authorization.SystemRoleProtected", "The system role is protected.");
    public static readonly Error RoleHasAssignedUsers = new(
        "Authorization.RoleHasAssignedUsers", "A role assigned to users cannot be deleted.");
    public static readonly Error UnknownPermission = new(
        "Authorization.UnknownPermission", "One or more permission identifiers are unknown.");
    public static readonly Error GrantCeilingExceeded = new(
        "Authorization.GrantCeilingExceeded", "The operation exceeds the actor's effective permissions.");
    public static readonly Error RoleDisabled = new(
        "Authorization.RoleDisabled", "A disabled role cannot be assigned.");
    public static readonly Error RoleAlreadyAssigned = new(
        "Authorization.RoleAlreadyAssigned", "The role is already assigned to this user.");
    public static readonly Error RoleNotAssigned = new(
        "Authorization.RoleNotAssigned", "The role is not assigned to this user.");
    public static readonly Error LastAdministratorRequired = new(
        "Authorization.LastAdministratorRequired", "The last viable Administrator assignment cannot be removed.");
    public static readonly Error InvalidActor = new(
        "Authorization.InvalidActor", "The authenticated actor no longer exists.");
    public static readonly Error ConcurrencyConflict = new(
        "Authorization.ConcurrencyConflict", "Authorization state changed during the operation.");
}
