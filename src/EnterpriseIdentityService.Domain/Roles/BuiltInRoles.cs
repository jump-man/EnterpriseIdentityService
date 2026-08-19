namespace EnterpriseIdentityService.Domain.Roles;

public static class BuiltInRoles
{
    public static readonly RoleId AdministratorId =
        new(new Guid("7d8f6e36-72a1-4f91-9b0f-8bf83ed7247c"));

    public const string AdministratorName = "Administrator";
}
