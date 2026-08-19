namespace EnterpriseIdentityService.Application.Authorization;

public static class Permissions
{
    public static class Roles
    {
        public const string Read = "roles.read";
        public const string Manage = "roles.manage";
    }

    public static class UserRoles
    {
        public const string Read = "users.roles.read";
        public const string Manage = "users.roles.manage";
    }

    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(
        new[]
        {
            Roles.Read,
            Roles.Manage,
            UserRoles.Read,
            UserRoles.Manage
        });

    public static bool Contains(string permission) =>
        All.Contains(permission, StringComparer.Ordinal);
}
