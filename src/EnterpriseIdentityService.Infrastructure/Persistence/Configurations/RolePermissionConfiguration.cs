using EnterpriseIdentityService.Application.Authorization;
using EnterpriseIdentityService.Domain.Roles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseIdentityService.Infrastructure.Persistence.Configurations;

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions", "identity");
        builder.HasKey(rolePermission => new
        {
            rolePermission.RoleId,
            rolePermission.Permission
        });
        builder.Property(rolePermission => rolePermission.RoleId)
            .HasConversion(id => id.Value, value => new RoleId(value));
        builder.Property(rolePermission => rolePermission.Permission)
            .HasMaxLength(100)
            .IsRequired();
        builder.HasIndex(rolePermission => rolePermission.Permission);

        builder.HasData(Permissions.All.Select(permission => new
        {
            RoleId = BuiltInRoles.AdministratorId,
            Permission = permission
        }));
    }
}
